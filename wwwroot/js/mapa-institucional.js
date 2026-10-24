// wwwroot/js/mapa-institucional.js
// Versión Clásica mejorada: Velas azules + Popup al hacer clic.

window.inicializarMapaInstitucional = async function (dotnetHelper, sedesData) {
    // 1. Cargar Leaflet
    if (!window.L) {
        await new Promise((resolve) => {
            const link = document.createElement('link');
            link.rel = 'stylesheet';
            link.href = 'https://unpkg.com/leaflet@1.9.4/dist/leaflet.css';
            document.head.appendChild(link);

            const script = document.createElement('script');
            script.src = 'https://unpkg.com/leaflet@1.9.4/dist/leaflet.js';
            script.onload = resolve;
            document.head.appendChild(script);
        });
    }

    await new Promise(r => setTimeout(r, 200));

    // Eliminar mapa previo si existe
    if (window._mapaInst) {
        window._mapaInst.remove();
        window._mapaInst = null;
    }

    // 2. Centro por defecto: Popayán
    const mapa = L.map('mapa-institucional').setView([2.4448, -76.6065], 13);
    window._mapaInst = mapa;

    // 3. Fondo de mapa OpenStreetMap
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '© OpenStreetMap',
        maxZoom: 19
    }).addTo(mapa);

    const bounds = [];

    // 4. Dibujar las velas azules
    sedesData.forEach(sede => {
        bounds.push([sede.lat, sede.lng]);

        const marker = L.marker([sede.lat, sede.lng]).addTo(mapa);

        // Creamos el contenido del globito con el nombre y la dirección
        const popupContent = `
            <div style="font-family: 'Segoe UI', sans-serif; text-align: center;">
                <strong style="color: #1e3a8a; font-size: 14px;">${sede.nombre}</strong><br>
                <span style="color: #64748b; font-size: 12px;">📍 ${sede.direccion || 'Sin dirección'}</span>
            </div>
        `;

        marker.bindPopup(popupContent);

        // Al hacer clic en la vela:
        marker.on('click', () => {
            // 1. Centra el mapa suavemente en esta vela
            mapa.setView([sede.lat, sede.lng], 16, { animate: true });

            // 2. Abre el globito
            marker.openPopup();

            // 3. Actualiza el panel de la derecha (Blazor)
            dotnetHelper.invokeMethodAsync('SeleccionarSedeEnMapa', sede.id);
        });
    });

    // 5. Ajustar el zoom inicial para que se vean todas las velas
    if (bounds.length > 0) {
        mapa.fitBounds(bounds, { padding: [50, 50] });
    }
};