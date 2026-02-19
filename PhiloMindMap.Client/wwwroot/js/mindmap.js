window.philoMindMap = window.philoMindMap || {};

window.generateDescriptionWithLlmApi = async (prompt, model) => {
    if (!prompt || !prompt.trim()) {
        throw new Error('Le prompt est vide.');
    }

    const allowedModels = new Set(['tinyllama', 'deepseek-r1:1.5b']);
    const selectedModel = allowedModels.has((model || '').trim())
        ? model.trim()
        : 'tinyllama';

    const response = await fetch('https://mlvoca.com/api/generate', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify({
            model: selectedModel,
            prompt: prompt.trim(),
            stream: false
        })
    });

    if (!response.ok) {
        throw new Error(`Erreur API LLM (${response.status}).`);
    }

    const payload = await response.json();

    if (payload && typeof payload.response === 'string') {
        return payload.response.trim();
    }

    if (typeof payload === 'string') {
        return payload.trim();
    }

    if (payload && typeof payload.message?.content === 'string') {
        return payload.message.content.trim();
    }

    if (payload && typeof payload.text === 'string') {
        return payload.text.trim();
    }

    return String(payload ?? '').trim();
};

window.initializeCytoscape = (dotNetReference, layout) => {
    const elements = JSON.parse(layout);
    const container = document.getElementById('cy');

    if (window.philoMindMap.cy) {
        window.philoMindMap.cy.destroy();
    }

    const cy = cytoscape({
        container,
        elements,
        style: [
            {
                selector: 'node[dataType = "idea"]',
                style: {
                    'shape': 'ellipse',
                    'background-image': 'url("/images/idea.jpg")',
                    'background-fit': 'cover',
                    'opacity': 1,
                    'label': 'data(label)',
                    'color': '#fff',
                    'text-outline-color': '#000',
                    'text-outline-width': 2,
                    'font-size': '12px',
                    'text-halign': 'center',
                    'text-valign': 'center',
                    'padding-left': '10px',
                    'padding-right': '10px',
                    'padding-top': '5px',
                    'padding-bottom': '5px',
                    'width': '50px',
                    'height': '50px'
                }
            },
            {
                selector: 'node[dataType = "philosoph"]',
                style: {
                    'shape': 'rectangle',
                    'background-image': 'data(imageUrl)',
                    'background-fit': 'contain',
                    'opacity': 0.8,
                    'label': 'data(label)',
                    'color': '#fff',
                    'text-outline-color': '#000',
                    'text-outline-width': 2,
                    'font-size': '12px',
                    'text-halign': 'center',
                    'text-valign': 'center',
                    'padding-left': '10px',
                    'padding-right': '10px',
                    'padding-top': '5px',
                    'padding-bottom': '5px',
                    'width': '45px',
                    'height': '53px'
                }
            },
            {
                selector: 'edge',
                style: {
                    'width': 3,
                    'line-color': '#ccc',
                    'target-arrow-color': '#ccc',
                    'target-arrow-shape': 'triangle'
                }
            },
            {
                selector: 'edge.highlight',
                style: {
                    'line-color': '#FF4136',
                    'target-arrow-color': '#FF4136',
                    'source-arrow-color': '#FF4136',
                    'width': 4
                }
            }
        ],
        layout: {
            name: 'preset'
        },
        wheelSensitivity: 0.08,
        zoomingEnabled: true,
        minZoom: 0.1,
        maxZoom: 2
    });

    window.philoMindMap.cy = cy;
    window.philoMindMap.dotNetReference = dotNetReference;

    attachMindMapEvents(cy, dotNetReference);
    ensureContextMenu(cy, dotNetReference);
};

window.reloadMindMap = (layout) => {
    const state = window.philoMindMap;
    if (!state || !state.cy) {
        return;
    }

    const elements = JSON.parse(layout);
    state.cy.batch(() => {
        state.cy.elements().remove();
        state.cy.add(elements);
    });
};

window.addNodeToMindMap = (nodeData) => {
    const state = window.philoMindMap;
    if (!state || !state.cy) {
        return;
    }

    const node = JSON.parse(nodeData);
    const existingNode = state.cy.getElementById(node.data.id);

    if (existingNode.length === 0) {
        state.cy.add(node);
    }
};

window.addEdgeToMindMap = (edgeData) => {
    const state = window.philoMindMap;
    if (!state || !state.cy) {
        return;
    }

    const edge = JSON.parse(edgeData);
    const existingEdge = state.cy.getElementById(edge.data.id);

    if (existingEdge.length === 0) {
        state.cy.add(edge);
    }
};

function attachMindMapEvents(cy, dotNetReference) {
    cy.on('mouseover', 'node', function (event) {
        const node = event.target;
        node.connectedEdges().addClass('highlight');
    });

    cy.on('mouseout', 'node', function (event) {
        const node = event.target;
        node.connectedEdges().removeClass('highlight');
    });

    cy.on('mouseover', 'edge', function (event) {
        const edge = event.target;
        edge.addClass('highlight');
    });

    cy.on('mouseout', 'edge', function (event) {
        const edge = event.target;
        edge.removeClass('highlight');
    });

    cy.on('tap', 'node', function (event) {
        const node = event.target;
        dotNetReference.invokeMethodAsync('OpenModal', node.id(), 'node', '', '').catch(function (error) {
            console.error('Error calling .NET method:', error);
        });
    });

    cy.on('tap', 'edge', function (event) {
        const edge = event.target;
        dotNetReference.invokeMethodAsync('OpenModal', edge.id(), 'edge', edge.source().id(), edge.target().id()).catch(function (error) {
            console.error('Error calling .NET method:', error);
        });
    });

    cy.on('dragfree', 'node', function (event) {
        const node = event.target;
        const position = node.position();
        const dataType = node.data('dataType');

        dotNetReference.invokeMethodAsync('SaveNodePosition', node.id(), position.x, position.y, dataType)
            .catch(function (error) {
                console.error('Error saving node position:', error);
            });
    });
}

function ensureContextMenu(cy, dotNetReference) {
    const existing = document.getElementById('cy-context-menu');
    if (existing) {
        existing.remove();
    }

    const menu = document.createElement('div');
    menu.id = 'cy-context-menu';
    menu.className = 'cy-context-menu';
    menu.style.display = 'none';

    const options = [
        { type: 'philosopher', label: 'Ajouter un philosophe' },
        { type: 'idea', label: 'Ajouter une idée' },
        { type: 'link', label: 'Ajouter un lien idée-philosophe' }
    ];

    options.forEach(function (option) {
        const item = document.createElement('button');
        item.type = 'button';
        item.className = 'cy-context-menu-item';
        item.setAttribute('data-type', option.type);
        item.textContent = option.label;
        item.addEventListener('click', function () {
            const clickX = Number(menu.dataset.graphX || 0);
            const clickY = Number(menu.dataset.graphY || 0);
            dotNetReference.invokeMethodAsync('OpenCreateModal', option.type, clickX, clickY).catch(function (error) {
                console.error('Error opening create modal:', error);
            });
            hideContextMenu();
        });
        menu.appendChild(item);
    });

    document.body.appendChild(menu);

    function hideContextMenu() {
        menu.style.display = 'none';
    }

    function showContextMenu(clientX, clientY, graphX, graphY) {
        menu.style.left = clientX + 'px';
        menu.style.top = clientY + 'px';
        menu.dataset.graphX = graphX;
        menu.dataset.graphY = graphY;
        menu.style.display = 'block';
    }

    cy.container().addEventListener('contextmenu', function (event) {
        event.preventDefault();
    });

    cy.on('cxttap', function (event) {
        const renderPos = event.renderedPosition || { x: 0, y: 0 };
        const graphPos = event.position || { x: 0, y: 0 };
        const rect = cy.container().getBoundingClientRect();

        showContextMenu(
            rect.left + renderPos.x,
            rect.top + renderPos.y,
            graphPos.x,
            graphPos.y
        );
    });

    cy.on('tap pan zoom', function () {
        hideContextMenu();
    });

    document.addEventListener('click', function (event) {
        if (!menu.contains(event.target)) {
            hideContextMenu();
        }
    });
}
