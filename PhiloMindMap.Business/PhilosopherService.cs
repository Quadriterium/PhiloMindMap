using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PhiloMindMap.Business.Data;
using PhiloMindMap.DTO;

namespace PhiloMindMap.Business
{
    public class PhilosopherService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly PhiloMindMapDbContext _dbContext;
        private readonly ILogger<PhilosopherService> _logger;
        private readonly SeedData _seedData;

        public PhilosopherService(
            PhiloMindMapDbContext dbContext,
            IHostEnvironment hostEnvironment,
            ILogger<PhilosopherService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
            _seedData = LoadSeedData(hostEnvironment.ContentRootPath);
        }

        public void InitializeDatabase()
        {
            _dbContext.Database.EnsureCreated();
            EnsureMindMapContentTable();
            EnsureLayoutColumns();
            EnsurePhilosopherProfileImageColumn();

            if (!_dbContext.Philosophers.Any())
            {
                _dbContext.Philosophers.AddRange(_seedData.Philosophers);
            }

            if (!_dbContext.Ideas.Any() || !_dbContext.PhilosopherIdeaLinks.Any())
            {
                SeedIdeasAndLinksFromLayout(_seedData.Layout);
            }

            SeedMindMapContents();

            _dbContext.SaveChanges();
        }

        public List<Philosopher> GetAll()
        {
            return _dbContext.Philosophers
                .AsNoTracking()
                .OrderBy(p => p.Name)
                .ToList();
        }

        public List<object> GetLayout()
        {
            var layout = new List<object>();

            var ideas = _dbContext.Ideas
                .AsNoTracking()
                .OrderBy(i => i.Name)
                .ToList();

            foreach (var idea in ideas)
            {
                layout.Add(new JsonObject
                {
                    ["data"] = new JsonObject
                    {
                        ["id"] = BuildSlug(idea.Name),
                        ["label"] = idea.Name,
                        ["dataType"] = "idea"
                    },
                    ["position"] = new JsonObject
                    {
                        ["x"] = idea.PositionX,
                        ["y"] = idea.PositionY
                    }
                });
            }

            var philosophers = _dbContext.Philosophers
                .AsNoTracking()
                .OrderBy(p => p.Name)
                .ToList();

            foreach (var philosopher in philosophers)
            {
                layout.Add(new JsonObject
                {
                    ["data"] = new JsonObject
                    {
                        ["id"] = BuildSlug(philosopher.Name),
                        ["label"] = philosopher.Name,
                        ["dataType"] = "philosoph",
                        ["imageUrl"] = string.IsNullOrWhiteSpace(philosopher.ProfileImageUrl)
                            ? "/images/philosoph.jpg"
                            : philosopher.ProfileImageUrl
                    },
                    ["position"] = new JsonObject
                    {
                        ["x"] = philosopher.PositionX,
                        ["y"] = philosopher.PositionY
                    }
                });
            }

            var edges = from link in _dbContext.PhilosopherIdeaLinks.AsNoTracking()
                        join philosopher in _dbContext.Philosophers.AsNoTracking() on link.PhilosopherId equals philosopher.Id
                        join idea in _dbContext.Ideas.AsNoTracking() on link.IdeaId equals idea.Id
                        select new
                        {
                            EdgeId = !string.IsNullOrWhiteSpace(link.RelationType)
                                ? link.RelationType
                                : $"{BuildSlug(idea.Name)}_{BuildSlug(philosopher.Name)}",
                            Source = BuildSlug(idea.Name),
                            Target = BuildSlug(philosopher.Name)
                        };

            foreach (var edge in edges)
            {
                layout.Add(new JsonObject
                {
                    ["data"] = new JsonObject
                    {
                        ["id"] = edge.EdgeId,
                        ["source"] = edge.Source,
                        ["target"] = edge.Target
                    }
                });
            }

            return layout;
        }

        public bool UpdateNodePosition(string? nodeId, string? dataType, double x, double y)
        {
            var normalizedNodeId = BuildSlug(nodeId);
            if (string.IsNullOrWhiteSpace(normalizedNodeId))
            {
                return false;
            }

            var normalizedType = dataType?.Trim().ToLowerInvariant();

            if (normalizedType == "idea")
            {
                var idea = _dbContext.Ideas
                    .AsEnumerable()
                    .FirstOrDefault(i => BuildSlug(i.Name) == normalizedNodeId);
                if (idea is null)
                {
                    return false;
                }

                idea.PositionX = x;
                idea.PositionY = y;
                _dbContext.SaveChanges();
                return true;
            }

            if (normalizedType == "philosoph")
            {
                var philosopher = _dbContext.Philosophers
                    .AsEnumerable()
                    .FirstOrDefault(p => BuildSlug(p.Name) == normalizedNodeId);
                if (philosopher is null)
                {
                    return false;
                }

                philosopher.PositionX = x;
                philosopher.PositionY = y;
                _dbContext.SaveChanges();
                return true;
            }

            var anyIdea = _dbContext.Ideas
                .AsEnumerable()
                .FirstOrDefault(i => BuildSlug(i.Name) == normalizedNodeId);
            if (anyIdea is not null)
            {
                anyIdea.PositionX = x;
                anyIdea.PositionY = y;
                _dbContext.SaveChanges();
                return true;
            }

            var anyPhilosopher = _dbContext.Philosophers
                .AsEnumerable()
                .FirstOrDefault(p => BuildSlug(p.Name) == normalizedNodeId);
            if (anyPhilosopher is not null)
            {
                anyPhilosopher.PositionX = x;
                anyPhilosopher.PositionY = y;
                _dbContext.SaveChanges();
                return true;
            }

            return false;
        }

        public MindMapContentView? GetMindMapContent(string? key)
        {
            var normalizedKey = BuildSlug(key);
            if (string.IsNullOrWhiteSpace(normalizedKey))
            {
                return null;
            }

            var content = _dbContext.MindMapContents
                .AsNoTracking()
                .FirstOrDefault(c => c.ContentKey == normalizedKey);

            if (content is null)
            {
                return null;
            }

            return new MindMapContentView
            {
                ContentKey = content.ContentKey,
                Title = content.Title,
                HtmlContent = content.HtmlContent
            };
        }

        public string? GetNodeDisplayName(string? nodeId)
        {
            var normalizedNodeId = BuildSlug(nodeId);
            if (string.IsNullOrWhiteSpace(normalizedNodeId))
            {
                return null;
            }

            var philosopherName = _dbContext.Philosophers
                .AsNoTracking()
                .AsEnumerable()
                .Where(p => BuildSlug(p.Name) == normalizedNodeId)
                .Select(p => p.Name)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(philosopherName))
            {
                return philosopherName;
            }

            var ideaName = _dbContext.Ideas
                .AsNoTracking()
                .AsEnumerable()
                .Where(i => BuildSlug(i.Name) == normalizedNodeId)
                .Select(i => i.Name)
                .FirstOrDefault();

            return string.IsNullOrWhiteSpace(ideaName) ? null : ideaName;
        }

        public PhilosopherNodeView? GetPhilosopherNodeById(string? nodeId)
        {
            var normalizedNodeId = BuildSlug(nodeId);
            if (string.IsNullOrWhiteSpace(normalizedNodeId))
            {
                return null;
            }

            var philosopher = _dbContext.Philosophers
                .AsNoTracking()
                .AsEnumerable()
                .FirstOrDefault(p => BuildSlug(p.Name) == normalizedNodeId);

            if (philosopher is null)
            {
                return null;
            }

            return new PhilosopherNodeView
            {
                Name = philosopher.Name,
                ProfileImageUrl = philosopher.ProfileImageUrl
            };
        }

        public void SavePhilosopherProfileImage(string? nodeId, string? profileImageUrl)
        {
            var normalizedNodeId = BuildSlug(nodeId);
            if (string.IsNullOrWhiteSpace(normalizedNodeId) || string.IsNullOrWhiteSpace(profileImageUrl))
            {
                return;
            }

            var philosopher = _dbContext.Philosophers
                .AsEnumerable()
                .FirstOrDefault(p => BuildSlug(p.Name) == normalizedNodeId);

            if (philosopher is null)
            {
                return;
            }

            philosopher.ProfileImageUrl = profileImageUrl.Trim();
            _dbContext.SaveChanges();
        }

        public MindMapContentView SaveMindMapContent(string? key, string? title, string? htmlContent)
        {
            var normalizedKey = BuildSlug(key);
            if (string.IsNullOrWhiteSpace(normalizedKey))
            {
                throw new InvalidOperationException("La clé de contenu est invalide.");
            }

            var normalizedTitle = string.IsNullOrWhiteSpace(title)
                ? BuildDisplayNameFromSlug(normalizedKey)
                : title.Trim();

            var normalizedHtml = htmlContent?.Trim() ?? string.Empty;

            var entity = _dbContext.MindMapContents
                .FirstOrDefault(c => c.ContentKey == normalizedKey);

            if (entity is null)
            {
                entity = new MindMapContent
                {
                    ContentKey = normalizedKey,
                    Title = normalizedTitle,
                    HtmlContent = normalizedHtml
                };

                _dbContext.MindMapContents.Add(entity);
            }
            else
            {
                entity.Title = normalizedTitle;
                entity.HtmlContent = normalizedHtml;
            }

            _dbContext.SaveChanges();

            return new MindMapContentView
            {
                ContentKey = entity.ContentKey,
                Title = entity.Title,
                HtmlContent = entity.HtmlContent
            };
        }

        public List<Philosopher> SearchPhilosophers(string? query)
        {
            var normalizedQuery = query?.Trim();

            var philosophers = _dbContext.Philosophers.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(normalizedQuery))
            {
                philosophers = philosophers.Where(p =>
                    EF.Functions.Like(p.Name, $"%{normalizedQuery}%") ||
                    EF.Functions.Like(p.Description, $"%{normalizedQuery}%"));
            }

            return philosophers
                .OrderBy(p => p.Name)
                .ToList();
        }

        public List<Idea> SearchIdeas(string? query)
        {
            var normalizedQuery = query?.Trim();

            var ideas = _dbContext.Ideas.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(normalizedQuery))
            {
                ideas = ideas.Where(i =>
                    EF.Functions.Like(i.Name, $"%{normalizedQuery}%") ||
                    (i.Description != null && EF.Functions.Like(i.Description, $"%{normalizedQuery}%")));
            }

            return ideas
                .OrderBy(i => i.Name)
                .ToList();
        }

        public List<PhilosopherIdeaLinkView> SearchLinks(long? philosopherId, long? ideaId)
        {
            var links = from link in _dbContext.PhilosopherIdeaLinks.AsNoTracking()
                        join philosopher in _dbContext.Philosophers.AsNoTracking() on link.PhilosopherId equals philosopher.Id
                        join idea in _dbContext.Ideas.AsNoTracking() on link.IdeaId equals idea.Id
                        select new PhilosopherIdeaLinkView
                        {
                            Id = link.Id,
                            PhilosopherId = philosopher.Id,
                            PhilosopherName = philosopher.Name,
                            IdeaId = idea.Id,
                            IdeaName = idea.Name,
                            RelationType = link.RelationType
                        };

            if (philosopherId.HasValue)
            {
                links = links.Where(l => l.PhilosopherId == philosopherId.Value);
            }

            if (ideaId.HasValue)
            {
                links = links.Where(l => l.IdeaId == ideaId.Value);
            }

            return links
                .OrderBy(l => l.PhilosopherName)
                .ThenBy(l => l.IdeaName)
                .ToList();
        }

        public Philosopher AddPhilosopher(Philosopher philosopher)
        {
            _dbContext.Philosophers.Add(philosopher);
            _dbContext.SaveChanges();
            return philosopher;
        }

        public Idea AddIdea(Idea idea)
        {
            _dbContext.Ideas.Add(idea);
            _dbContext.SaveChanges();
            return idea;
        }

        public PhilosopherIdeaLink AddLink(long philosopherId, long ideaId, string? relationType)
        {
            var philosopherExists = _dbContext.Philosophers.Any(p => p.Id == philosopherId);
            var ideaExists = _dbContext.Ideas.Any(i => i.Id == ideaId);

            if (!philosopherExists || !ideaExists)
            {
                throw new InvalidOperationException("Le philosophe ou l'idée n'existe pas.");
            }

            var existing = _dbContext.PhilosopherIdeaLinks
                .FirstOrDefault(l => l.PhilosopherId == philosopherId && l.IdeaId == ideaId);

            if (existing is not null)
            {
                existing.RelationType = relationType;
                _dbContext.SaveChanges();
                return existing;
            }

            var link = new PhilosopherIdeaLink
            {
                PhilosopherId = philosopherId,
                IdeaId = ideaId,
                RelationType = relationType
            };

            _dbContext.PhilosopherIdeaLinks.Add(link);
            _dbContext.SaveChanges();
            return link;
        }

        private void SeedIdeasAndLinksFromLayout(List<JsonObject> layout)
        {
            var philosophers = _dbContext.Philosophers.ToList();
            var philosophersBySlug = philosophers
                .ToDictionary(p => BuildSlug(p.Name), p => p, StringComparer.OrdinalIgnoreCase);

            var existingIdeas = _dbContext.Ideas.ToList();
            var ideaBySlug = new Dictionary<string, Idea>(StringComparer.OrdinalIgnoreCase);

            foreach (var existingIdea in existingIdeas)
            {
                ideaBySlug[BuildSlug(existingIdea.Name)] = existingIdea;
                ideaBySlug[existingIdea.Name] = existingIdea;
            }

            foreach (var node in layout)
            {
                var data = node["data"] as JsonObject;
                if (data is null)
                {
                    continue;
                }

                var label = data["label"]?.GetValue<string>();
                var id = data["id"]?.GetValue<string>();
                var dataType = data["dataType"]?.GetValue<string>();

                var position = node["position"] as JsonObject;
                var x = position?["x"]?.GetValue<double>() ?? 0d;
                var y = position?["y"]?.GetValue<double>() ?? 0d;

                if (string.Equals(dataType, "philosoph", StringComparison.OrdinalIgnoreCase))
                {
                    var philosopherKey = !string.IsNullOrWhiteSpace(id) ? BuildSlug(id) : BuildSlug(label);
                    if (!string.IsNullOrWhiteSpace(philosopherKey)
                        && philosophersBySlug.TryGetValue(philosopherKey, out var philosopher))
                    {
                        philosopher.PositionX = x;
                        philosopher.PositionY = y;
                    }

                    continue;
                }

                if (!string.Equals(dataType, "idea", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(label))
                {
                    continue;
                }

                var labelSlug = BuildSlug(label);
                var idSlug = BuildSlug(id);
                if (!ideaBySlug.TryGetValue(labelSlug, out var idea))
                {
                    idea = new Idea
                    {
                        Name = label,
                        Description = null,
                        PositionX = x,
                        PositionY = y
                    };

                    _dbContext.Ideas.Add(idea);
                    _dbContext.SaveChanges();
                }
                else
                {
                    idea.PositionX = x;
                    idea.PositionY = y;
                }

                ideaBySlug[labelSlug] = idea;
                if (!string.IsNullOrWhiteSpace(idSlug))
                {
                    ideaBySlug[idSlug] = idea;
                }

                if (!string.IsNullOrWhiteSpace(id))
                {
                    ideaBySlug[id] = idea;
                }
            }

            _dbContext.SaveChanges();

            var philosopherIdsBySlug = _dbContext.Philosophers
                .AsNoTracking()
                .ToDictionary(p => BuildSlug(p.Name), p => p.Id, StringComparer.OrdinalIgnoreCase);

            var existingLinks = _dbContext.PhilosopherIdeaLinks
                .AsNoTracking()
                .Select(l => new { l.PhilosopherId, l.IdeaId })
                .ToHashSet();

            foreach (var edge in layout)
            {
                var data = edge["data"] as JsonObject;
                if (data is null)
                {
                    continue;
                }

                var source = data["source"]?.GetValue<string>();
                var target = data["target"]?.GetValue<string>();

                if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
                {
                    continue;
                }

                if (!ideaBySlug.TryGetValue(BuildSlug(source), out var idea)
                    && !ideaBySlug.TryGetValue(source, out idea))
                {
                    continue;
                }

                if (!philosopherIdsBySlug.TryGetValue(BuildSlug(target), out var philosopherId)
                    && !philosopherIdsBySlug.TryGetValue(target, out philosopherId))
                {
                    continue;
                }

                var linkKey = new { PhilosopherId = philosopherId, IdeaId = idea.Id };

                if (existingLinks.Contains(linkKey))
                {
                    continue;
                }

                _dbContext.PhilosopherIdeaLinks.Add(new PhilosopherIdeaLink
                {
                    PhilosopherId = philosopherId,
                    IdeaId = idea.Id,
                    RelationType = data["id"]?.GetValue<string>()
                });

                existingLinks.Add(linkKey);
            }
        }

        private void EnsureLayoutColumns()
        {
            if (!ColumnExists("Philosophers", "PositionX"))
            {
                _dbContext.Database.ExecuteSqlRaw("ALTER TABLE Philosophers ADD COLUMN PositionX REAL NOT NULL DEFAULT 0;");
            }

            if (!ColumnExists("Philosophers", "PositionY"))
            {
                _dbContext.Database.ExecuteSqlRaw("ALTER TABLE Philosophers ADD COLUMN PositionY REAL NOT NULL DEFAULT 0;");
            }

            if (!ColumnExists("Ideas", "PositionX"))
            {
                _dbContext.Database.ExecuteSqlRaw("ALTER TABLE Ideas ADD COLUMN PositionX REAL NOT NULL DEFAULT 0;");
            }

            if (!ColumnExists("Ideas", "PositionY"))
            {
                _dbContext.Database.ExecuteSqlRaw("ALTER TABLE Ideas ADD COLUMN PositionY REAL NOT NULL DEFAULT 0;");
            }
        }

        private void EnsurePhilosopherProfileImageColumn()
        {
            if (!ColumnExists("Philosophers", "ProfileImageUrl"))
            {
                _dbContext.Database.ExecuteSqlRaw("ALTER TABLE Philosophers ADD COLUMN ProfileImageUrl TEXT NULL;");
            }
        }

        private bool ColumnExists(string tableName, string columnName)
        {
            var connection = _dbContext.Database.GetDbConnection();
            var shouldClose = connection.State != System.Data.ConnectionState.Open;

            if (shouldClose)
            {
                connection.Open();
            }

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = $"PRAGMA table_info({tableName});";

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    if (string.Equals(reader["name"]?.ToString(), columnName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }
            finally
            {
                if (shouldClose)
                {
                    connection.Close();
                }
            }
        }

        private void EnsureMindMapContentTable()
        {
            _dbContext.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS MindMapContents (
                    Id INTEGER NOT NULL CONSTRAINT PK_MindMapContents PRIMARY KEY AUTOINCREMENT,
                    ContentKey TEXT NOT NULL,
                    Title TEXT NOT NULL,
                    HtmlContent TEXT NOT NULL
                );
            ");

            _dbContext.Database.ExecuteSqlRaw(@"
                CREATE UNIQUE INDEX IF NOT EXISTS IX_MindMapContents_ContentKey
                ON MindMapContents (ContentKey);
            ");
        }

        private void SeedMindMapContents()
        {
            var existingKeys = _dbContext.MindMapContents
                .AsNoTracking()
                .Select(c => c.ContentKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var content in GetDefaultMindMapContents())
            {
                if (existingKeys.Contains(content.ContentKey))
                {
                    continue;
                }

                _dbContext.MindMapContents.Add(content);
                existingKeys.Add(content.ContentKey);
            }
        }

        private static List<MindMapContent> GetDefaultMindMapContents()
        {
            return
            [
                new MindMapContent
                {
                    ContentKey = "spinoza",
                    Title = "Baruch Spinoza",
                    HtmlContent = @"
<p>
        Baruch Spinoza, philosophe né en 1632 à Amsterdam, est l'une des figures majeures de la philosophie occidentale.
        Dans son œuvre principale, <em>Éthique</em>, il développe une vision moniste de la réalité, affirmant que Dieu et la Nature sont une seule et même entité.
        Spinoza rejette l'idée d'un Dieu personnel et propose une conception panthéiste de la divinité.
    </p>
    <p>
        Il soutient que la connaissance véritable réside dans la compréhension des lois naturelles et de notre place dans l'univers.
        La raison, selon lui, est le moyen d'atteindre la liberté et la béatitude.
        Spinoza défend également l'idée que les émotions peuvent être comprises et maîtrisées par la raison, conduisant ainsi à une vie éthique.
        Son influence s'étend à des domaines tels que la politique, la théologie et la science, inspirant des penseurs modernes.
        Spinoza meurt en 1677, laissant un héritage intellectuel durable.
    </p>"
                },
                new MindMapContent
                {
                    ContentKey = "kant",
                    Title = "Emmanuel Kant",
                    HtmlContent = @"
 <p>
        Immanuel Kant, philosophe allemand né en 1724, est une figure centrale de la philosophie moderne.
        Dans son œuvre majeure, <em>Critique de la raison pure</em>, il explore les limites et les possibilités de la connaissance humaine.
        Kant distingue entre les choses telles qu'elles sont en elles-mêmes (noumènes) et les choses telles qu'elles apparaissent (phénomènes), posant ainsi les bases de l'idéalisme transcendantal.
        Il affirme que notre compréhension du monde est médiée par des structures mentales a priori, telles que l'espace et le temps.
    </p>
    <p>
        En éthique, Kant propose une approche déontologique fondée sur le devoir et la moralité universelle, exprimée par son célèbre impératif catégorique.
        Selon lui, la moralité doit être fondée sur des principes rationnels et non sur des conséquences.
        Sa philosophie politique, notamment dans <em>La paix perpétuelle</em>, met l'accent sur l'importance des droits humains et de la liberté.
        Kant meurt en 1804, laissant un impact profond sur la philosophie, la théologie et la théorie politique.
    </p>"
                },
                new MindMapContent
                {
                    ContentKey = "determinism",
                    Title = "Déterminisme",
                    HtmlContent = @"
<p>
        Le déterminisme est la théorie philosophique selon laquelle tous les événements, y compris les actions humaines, sont causés par des conditions antérieures.
        Selon cette perspective, chaque événement est le résultat inévitable de lois naturelles et de causes précédentes.
        Le déterminisme se décline en plusieurs formes, notamment le déterminisme scientifique, qui postule que l'univers fonctionne selon des lois physiques précises.
    </p>
    <p>
        En revanche, le déterminisme psychologique examine comment nos pensées, émotions et comportements sont influencés par des facteurs biologiques et environnementaux.
        Une question clé du déterminisme est celle de la liberté : si tout est déterminé, peut-on vraiment être considéré comme libre dans nos choix ?
        Les débats autour du déterminisme impliquent souvent des discussions sur la responsabilité morale et l'autonomie personnelle.
        Bien que certains philosophes soutiennent que le déterminisme et la liberté peuvent coexister, d'autres, appelés libertariens, rejettent l'idée d'un univers entièrement déterminé.
        Le déterminisme a des implications importantes pour la science, la philosophie et la compréhension de la nature humaine.
    </p>"
                },
                new MindMapContent
                {
                    ContentKey = "freewill",
                    Title = "Libre arbitre",
                    HtmlContent = @"
<p>
        Le libre arbitre est le concept selon lequel les individus ont la capacité de faire des choix indépendamment des influences extérieures.
        Cette notion est essentielle dans les discussions sur la responsabilité morale et l'éthique.
        Les partisans du libre arbitre soutiennent que les êtres humains peuvent agir selon leur volonté, même face à des circonstances déterminantes.
    </p>
    <p>
        Il existe plusieurs perspectives sur le libre arbitre, notamment le compatibilisme, qui affirme que le libre arbitre et le déterminisme peuvent coexister.
        En revanche, les libertariens soutiennent que pour que le libre arbitre existe, il doit y avoir une absence de déterminisme.
        Les critiques du libre arbitre, tels que les déterministes, affirment que nos choix sont en réalité le produit de facteurs biologiques, psychologiques et environnementaux.
        Ce débat soulève des questions sur l'identité personnelle et la nature de l'esprit.
        En fin de compte, la question du libre arbitre reste l'une des plus débattues en philosophie, psychologie et théologie.
    </p>
"
                },
                new MindMapContent
                {
                    ContentKey = "ac",
                    Title = "Kant et le libre arbitre",
                    HtmlContent = @"
<p>
        Immanuel Kant aborde la question du libre arbitre dans le contexte de sa philosophie morale et de son idéalisme transcendantal.
        Selon Kant, pour que les actions humaines soient moralement responsables, elles doivent être basées sur le libre arbitre.
        Dans son œuvre <em>Critique de la raison pratique</em>, il affirme que la moralité implique que les individus puissent agir selon des lois qu'ils se donnent eux-mêmes.
        Cette notion de législation morale personnelle est essentielle pour Kant, car elle est liée à la dignité humaine.
    </p>
    <p>
        Kant soutient que le libre arbitre est compatible avec un certain déterminisme, notamment lorsqu'il s'agit des lois morales.
        Il postule que même si nos actions peuvent être influencées par des facteurs externes, nous avons la capacité de transcender ces influences à travers la raison.
        Ainsi, le libre arbitre chez Kant ne signifie pas l'absence de détermination, mais plutôt la capacité de choisir conformément à des principes moraux universels.
        Ce lien entre libre arbitre et moralité reste fondamental dans la pensée éthique moderne.
    </p>"
                },
                new MindMapContent
                {
                    ContentKey = "ad",
                    Title = "Libre arbitre et déterminisme",
                    HtmlContent = @"
<p>
        Le libre arbitre et le déterminisme représentent deux concepts opposés dans le débat philosophique sur la nature des choix humains.
        Le déterminisme soutient que tous les événements, y compris les actions humaines, sont causés par des facteurs antérieurs et des lois naturelles.
        Par conséquent, si nos actions sont entièrement déterminées, la notion de libre arbitre devient problématique, remettant en question notre responsabilité morale.
    </p>
    <p>
        Les partisans du libre arbitre, en revanche, soutiennent que les individus ont la capacité de choisir librement, indépendamment des influences externes.
        Cette tension entre les deux concepts a donné lieu à plusieurs positions philosophiques, comme le compatibilisme, qui tente de réconcilier les deux.
        Les compatibilistes affirment que le libre arbitre peut exister même dans un cadre déterministe, tant que les actions sont en accord avec les désirs et les intentions de l'individu.
        Les libertariens, quant à eux, rejettent le déterminisme, arguant que pour que le libre arbitre existe, il doit y avoir une liberté totale de choix.
        Cette opposition entre libre arbitre et déterminisme soulève des questions profondes sur la nature humaine, la moralité et la responsabilité.
    </p>"
                }
            ];
        }

        private SeedData LoadSeedData(string contentRootPath)
        {
            var seedFilePath = Path.Combine(contentRootPath, "SeedData", "philosophy-seed.json");

            if (!File.Exists(seedFilePath))
            {
                _logger.LogWarning("Fichier de seed introuvable: {SeedFilePath}", seedFilePath);
                return GetFallbackSeedData();
            }

            try
            {
                using var file = File.OpenRead(seedFilePath);
                var seedData = JsonSerializer.Deserialize<SeedData>(file, JsonOptions);

                if (seedData is null || seedData.Philosophers.Count == 0 || seedData.Layout.Count == 0)
                {
                    _logger.LogWarning("Fichier de seed vide ou invalide: {SeedFilePath}", seedFilePath);
                    return GetFallbackSeedData();
                }

                _logger.LogInformation(
                    "Seed chargé: {PhilosopherCount} philosophes, {LayoutCount} éléments de layout.",
                    seedData.Philosophers.Count,
                    seedData.Layout.Count);

                return seedData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du chargement du seed: {SeedFilePath}", seedFilePath);
                return GetFallbackSeedData();
            }
        }

        private static SeedData GetFallbackSeedData()
        {
            return new SeedData
            {
                Philosophers = new List<Philosopher>
                {
                    new()
                    {
                        Id = 1,
                        Name = "Socrate",
                        BirthDate = new DateTime(470, 1, 1),
                        DeathDate = new DateTime(399, 1, 1),
                        Description = "Socrate est l'un des fondateurs de la philosophie occidentale."
                    }
                },
                Layout = new List<JsonObject>
                {
                    new()
                    {
                        ["data"] = new JsonObject
                        {
                            ["id"] = "socrate",
                            ["label"] = "Socrate",
                            ["dataType"] = "philosoph"
                        },
                        ["position"] = new JsonObject
                        {
                            ["x"] = 100,
                            ["y"] = 100
                        }
                    }
                }
            };
        }

        private sealed class SeedData
        {
            public List<Philosopher> Philosophers { get; set; } = new();

            public List<JsonObject> Layout { get; set; } = new();
        }

        public sealed class PhilosopherIdeaLinkView
        {
            public long Id { get; set; }

            public long PhilosopherId { get; set; }

            public string PhilosopherName { get; set; } = string.Empty;

            public long IdeaId { get; set; }

            public string IdeaName { get; set; } = string.Empty;

            public string? RelationType { get; set; }
        }

        public sealed class MindMapContentView
        {
            public string ContentKey { get; set; } = string.Empty;

            public string Title { get; set; } = string.Empty;

            public string HtmlContent { get; set; } = string.Empty;
        }

        public sealed class PhilosopherNodeView
        {
            public string Name { get; set; } = string.Empty;

            public string? ProfileImageUrl { get; set; }
        }

        private static string BuildSlug(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var normalized = value.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);

            foreach (var c in normalized)
            {
                var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (category == System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(char.ToLowerInvariant(c));
                }
                else if (char.IsWhiteSpace(c) || c == '-' || c == '_')
                {
                    builder.Append('_');
                }
            }

            return builder.ToString().Trim('_');
        }

        private static string BuildDisplayNameFromSlug(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return string.Empty;
            }

            var chunks = slug.Replace('-', ' ').Replace('_', ' ')
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", chunks.Select(chunk => char.ToUpperInvariant(chunk[0]) + chunk[1..]));
        }
    }
}
