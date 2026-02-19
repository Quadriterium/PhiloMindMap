using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PhiloMindMap.Business;
using PhiloMindMap.DTO;
using System.Net;
using System.Text.Json;

namespace PhiloMindMap.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PhilosopherController : ControllerBase
    {
        private readonly PhilosopherService _svcPhilosoph;
        private readonly ILogger<PhilosopherController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public PhilosopherController(
            PhilosopherService svcPhilosoph,
            ILogger<PhilosopherController> logger,
            IHttpClientFactory httpClientFactory)
        {
            _svcPhilosoph = svcPhilosoph;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet]
        public IActionResult GetAll()
        {
            var data = _svcPhilosoph.GetAll();
            _logger.LogInformation("GetAll called");
            return Ok(data);
        }

        [HttpGet("search")]
        public IActionResult SearchPhilosophers([FromQuery] string? query)
        {
            var data = _svcPhilosoph.SearchPhilosophers(query);
            return Ok(data);
        }

        [HttpGet("ideas")]
        public IActionResult SearchIdeas([FromQuery] string? query)
        {
            var data = _svcPhilosoph.SearchIdeas(query);
            return Ok(data);
        }

        [HttpGet("links")]
        public IActionResult SearchLinks([FromQuery] long? philosopherId, [FromQuery] long? ideaId)
        {
            var data = _svcPhilosoph.SearchLinks(philosopherId, ideaId);
            return Ok(data);
        }

        [HttpPost]
        public IActionResult AddPhilosopher([FromBody] CreatePhilosopherRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest("Le nom du philosophe est requis.");
            }

            var created = _svcPhilosoph.AddPhilosopher(new Philosopher
            {
                Name = request.Name.Trim(),
                Description = request.Description?.Trim() ?? string.Empty,
                BirthDate = request.BirthDate,
                DeathDate = request.DeathDate
            });

            return Ok(created);
        }

        [HttpPost("ideas")]
        public IActionResult AddIdea([FromBody] CreateIdeaRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest("Le nom de l'idée est requis.");
            }

            var created = _svcPhilosoph.AddIdea(new Idea
            {
                Name = request.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(request.Description)
                    ? null
                    : request.Description.Trim()
            });

            return Ok(created);
        }

        [HttpPost("links")]
        public IActionResult AddLink([FromBody] CreateLinkRequest request)
        {
            try
            {
                var created = _svcPhilosoph.AddLink(request.PhilosopherId, request.IdeaId, request.RelationType);
                return Ok(created);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("layout")]
        public IActionResult GetLayout()
        {
            var data = _svcPhilosoph.GetLayout();
            _logger.LogInformation("GetLayout called");
            return Ok(data);
        }

        [HttpPut("layout/node-position")]
        public IActionResult UpdateNodePosition([FromBody] UpdateNodePositionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.NodeId))
            {
                return BadRequest("Le champ 'nodeId' est requis.");
            }

            var updated = _svcPhilosoph.UpdateNodePosition(request.NodeId, request.DataType, request.X, request.Y);
            if (!updated)
            {
                return NotFound();
            }

            return NoContent();
        }

        [HttpGet("content")]
        public async Task<IActionResult> GetContent([FromQuery] string id, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest("Le paramètre 'id' est requis.");
            }

            var philosopherNode = _svcPhilosoph.GetPhilosopherNodeById(id);
            var data = _svcPhilosoph.GetMindMapContent(id);
            if (data is not null)
            {
                if (philosopherNode is not null && string.IsNullOrWhiteSpace(philosopherNode.ProfileImageUrl))
                {
                    var imageSummary = await TryGetWikipediaSummaryAsync("fr", philosopherNode.Name, cancellationToken)
                                      ?? await TryGetWikipediaSummaryAsync("en", philosopherNode.Name, cancellationToken);

                    if (!string.IsNullOrWhiteSpace(imageSummary?.ThumbnailUrl))
                    {
                        _svcPhilosoph.SavePhilosopherProfileImage(id, imageSummary!.ThumbnailUrl);
                    }
                }

                return Ok(data);
            }

            var displayName = _svcPhilosoph.GetNodeDisplayName(id);
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return NotFound();
            }

            var summary = await TryGetWikipediaSummaryAsync("fr", displayName, cancellationToken)
                          ?? await TryGetWikipediaSummaryAsync("en", displayName, cancellationToken);

            if (summary is null || string.IsNullOrWhiteSpace(summary.Summary))
            {
                return NotFound();
            }

            var encodedSummary = System.Net.WebUtility.HtmlEncode(summary.Summary).Replace("\n", "<br />");
            var sourceLink = !string.IsNullOrWhiteSpace(summary.Url)
                ? $"<p><a href=\"{summary.Url}\" target=\"_blank\" rel=\"noopener noreferrer\">Source Wikipédia</a></p>"
                : string.Empty;
            var html = $"<p>{encodedSummary}</p>{sourceLink}";

            var saved = _svcPhilosoph.SaveMindMapContent(id, summary.Title, html);

            if (philosopherNode is not null && !string.IsNullOrWhiteSpace(summary.ThumbnailUrl))
            {
                _svcPhilosoph.SavePhilosopherProfileImage(id, summary.ThumbnailUrl);
            }

            return Ok(saved);
        }

        [HttpPut("content")]
        public IActionResult UpdateContent([FromBody] UpdateContentRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Id))
            {
                return BadRequest("Le champ 'id' est requis.");
            }

            var normalizedHtmlContent = request.HtmlContent?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(normalizedHtmlContent))
            {
                return BadRequest("Le contenu de la fiche ne peut pas être vide.");
            }

            var existing = _svcPhilosoph.GetMindMapContent(request.Id);
            var title = existing?.Title;

            if (string.IsNullOrWhiteSpace(title))
            {
                title = _svcPhilosoph.GetNodeDisplayName(request.Id);
            }

            var updated = _svcPhilosoph.SaveMindMapContent(request.Id, title, normalizedHtmlContent);
            return Ok(updated);
        }

        [HttpGet("wikipedia-summary")]
        public async Task<IActionResult> GetWikipediaSummary([FromQuery] string name, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest("Le paramètre 'name' est requis.");
            }

            var summary = await TryGetWikipediaSummaryAsync("fr", name, cancellationToken)
                          ?? await TryGetWikipediaSummaryAsync("en", name, cancellationToken);

            if (summary is null)
            {
                return NotFound();
            }

            return Ok(summary);
        }

        private async Task<WikipediaSummaryResponse?> TryGetWikipediaSummaryAsync(string language, string philosopherName, CancellationToken cancellationToken)
        {
            var client = _httpClientFactory.CreateClient();
            var normalizedTitle = philosopherName.Replace(' ', '_');
            var url = $"https://{language}.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(normalizedTitle)}";

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.UserAgent.ParseAdd("PhiloMindMap/1.0 (+https://localhost; educational)");
                request.Headers.Accept.ParseAdd("application/json");

                using var response = await client.SendAsync(request, cancellationToken);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }

                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    return await TryGetWikipediaSummaryViaActionApiAsync(language, philosopherName, cancellationToken);
                }

                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                if (!root.TryGetProperty("extract", out var extractProperty))
                {
                    return null;
                }

                var summaryText = extractProperty.GetString();
                if (string.IsNullOrWhiteSpace(summaryText))
                {
                    return null;
                }

                var title = root.TryGetProperty("title", out var titleProperty)
                    ? titleProperty.GetString() ?? philosopherName
                    : philosopherName;

                string? pageUrl = null;
                if (root.TryGetProperty("content_urls", out var urlsProperty)
                    && urlsProperty.TryGetProperty("desktop", out var desktopProperty)
                    && desktopProperty.TryGetProperty("page", out var pageProperty))
                {
                    pageUrl = pageProperty.GetString();
                }

                string? thumbnailUrl = null;
                if (root.TryGetProperty("originalimage", out var originalImageProperty)
                    && originalImageProperty.TryGetProperty("source", out var originalSourceProperty))
                {
                    thumbnailUrl = originalSourceProperty.GetString();
                }
                else if (root.TryGetProperty("thumbnail", out var thumbnailProperty)
                    && thumbnailProperty.TryGetProperty("source", out var thumbnailSourceProperty))
                {
                    thumbnailUrl = thumbnailSourceProperty.GetString();
                }

                return new WikipediaSummaryResponse
                {
                    Title = title,
                    Summary = summaryText,
                    Language = language,
                    Url = pageUrl,
                    ThumbnailUrl = thumbnailUrl
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Impossible de récupérer le résumé Wikipédia pour {Name} ({Language}).", philosopherName, language);
                return null;
            }
        }

        private async Task<WikipediaSummaryResponse?> TryGetWikipediaSummaryViaActionApiAsync(string language, string philosopherName, CancellationToken cancellationToken)
        {
            var client = _httpClientFactory.CreateClient();
            var actionApiUrl =
                $"https://{language}.wikipedia.org/w/api.php?action=query&prop=extracts|pageimages&pithumbsize=600&exintro=1&explaintext=1&redirects=1&formatversion=2&format=json&titles={Uri.EscapeDataString(philosopherName)}";

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, actionApiUrl);
                request.Headers.UserAgent.ParseAdd("PhiloMindMap/1.0 (+https://localhost; educational)");
                request.Headers.Accept.ParseAdd("application/json");

                using var response = await client.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                if (!root.TryGetProperty("query", out var query)
                    || !query.TryGetProperty("pages", out var pages)
                    || pages.ValueKind != JsonValueKind.Array
                    || pages.GetArrayLength() == 0)
                {
                    return null;
                }

                var page = pages[0];

                if (!page.TryGetProperty("extract", out var extractProperty))
                {
                    return null;
                }

                var summaryText = extractProperty.GetString();
                if (string.IsNullOrWhiteSpace(summaryText))
                {
                    return null;
                }

                var title = page.TryGetProperty("title", out var titleProperty)
                    ? titleProperty.GetString() ?? philosopherName
                    : philosopherName;

                var pageTitle = title.Replace(' ', '_');
                var pageUrl = $"https://{language}.wikipedia.org/wiki/{Uri.EscapeDataString(pageTitle)}";

                string? thumbnailUrl = null;
                if (page.TryGetProperty("thumbnail", out var thumbnailProperty)
                    && thumbnailProperty.TryGetProperty("source", out var thumbnailSourceProperty))
                {
                    thumbnailUrl = thumbnailSourceProperty.GetString();
                }

                return new WikipediaSummaryResponse
                {
                    Title = title,
                    Summary = summaryText,
                    Language = language,
                    Url = pageUrl,
                    ThumbnailUrl = thumbnailUrl
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fallback Action API en échec pour {Name} ({Language}).", philosopherName, language);
                return null;
            }
        }

        private sealed class WikipediaSummaryResponse
        {
            public string Title { get; set; } = string.Empty;

            public string Summary { get; set; } = string.Empty;

            public string Language { get; set; } = string.Empty;

            public string? Url { get; set; }

            public string? ThumbnailUrl { get; set; }
        }

        public sealed class CreatePhilosopherRequest
        {
            public string Name { get; set; } = string.Empty;

            public string? Description { get; set; }

            public DateTime BirthDate { get; set; }

            public DateTime? DeathDate { get; set; }
        }

        public sealed class CreateIdeaRequest
        {
            public string Name { get; set; } = string.Empty;

            public string? Description { get; set; }
        }

        public sealed class CreateLinkRequest
        {
            public long PhilosopherId { get; set; }

            public long IdeaId { get; set; }

            public string? RelationType { get; set; }
        }

        public sealed class UpdateNodePositionRequest
        {
            public string NodeId { get; set; } = string.Empty;

            public string? DataType { get; set; }

            public double X { get; set; }

            public double Y { get; set; }
        }

        public sealed class UpdateContentRequest
        {
            public string Id { get; set; } = string.Empty;

            public string? HtmlContent { get; set; }
        }
    }
}
