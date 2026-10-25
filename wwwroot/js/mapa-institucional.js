// wwwroot/js/mapa-institucional.js
// Versión Final: Popup detallado al hacer clic + Tooltip (nombre) al pasar el mouse.

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

        // ✅ NUEVO: Tooltip (aviso) al pasar el mouse por encima
        marker.bindTooltip(sede.nombre, {
            direction: 'top',      // Aparece arriba de la vela
            offset: [0, -35],      // Ajusta la altura para que no tape la vela
            opacity: 0.9,          // Un poquito de transparencia
            className: 'tooltip-sede' // Le ponemos una clase para darle estilo
        });

        // Formatear números para que se vean como $4.450.000
        const formatMoneda = (valor) => new Intl.NumberFormat('es-CO').format(valor || 0);

        // Contenido del globo (Popup) al hacer clic
        const popupContent = `
            <div style="font-family: 'Segoe UI', sans-serif; min-width: 200px;">
                <div style="text-align: center; margin-bottom: 8px;">
                    <strong style="color: #1e3a8a; font-size: 14px;">${sede.nombre}</strong><br>
                    <span style="color: #64748b; font-size: 11px;">📍 ${sede.direccion || 'Sin dirección'}</span>
                </div>
                
                <hr style="margin: 8px 0; border: 0; border-top: 1px solid #e2e8f0;">
                
                <div style="display: flex; justify-content: space-between; margin-bottom: 4px;">
                    <span style="font-size: 12px; color: #475569;">Bienes:</span>
                    <strong style="font-size: 12px; color: #0f172a;">${sede.bienes || 0}</strong>
                </div>
                <div style="display: flex; justify-content: space-between; margin-bottom: 4px;">
                    <span style="font-size: 12px; color: #475569;">Valor PPE:</span>
                    <strong style="font-size: 12px; color: #059669;">$${formatMoneda(sede.ppe)}</strong>
                </div>
                <div style="display: flex; justify-content: space-between;">
                    <span style="font-size: 12px; color: #475569;">Consumo:</span>
                    <strong style="font-size: 12px; color: #d97706;">$${formatMoneda(sede.consumo)}</strong>
                </div>
                
                <div style="text-align: center; margin-top: 8px; font-size: 10px; color: #94a3b8; font-style: italic;">
                    Clic para ver en el panel derecho →
                </div>
            </div>
        `;

        marker.bindPopup(popupContent, { maxWidth: 250 });

        // Al hacer clic en la vela:
        marker.on('click', () => {
            mapa.setView([sede.lat, sede.lng], 16, { animate: true });
            marker.openPopup();
            dotnetHelper.invokeMethodAsync('SeleccionarSedeEnMapa', sede.id);
        });
    });

    // 5. Ajustar el zoom inicial
    if (bounds.length > 0) {
        mapa.fitBounds(bounds, { padding: [50, 50] });
    }
};

// ✅ ESTILO PARA EL TOOLTIP (Que se vea bonito y no como un cuadro blanco feo)
const style = document.createElement('style');
style.innerHTML = `
    .tooltip-sede {
        background-color: #1e3a8a !important; /* Color azul oscuro elegante */
        color: white !important;
        border: none !important;
        border-radius: 6px !important;
        padding: 6px 12px !important;
        font-family: 'Segoe UI', sans-serif !important;
        font-weight: 600 !important;
        font-size: 12px !important;
        box-shadow: 0 4px 6px rgba(0,0,0,0.2) !important;
    }
    /* Flechita del tooltip */
    .tooltip-sede::before {
        border-top-color: #1e3a8a !important;
    }
`;
document.head.appendChild(style);