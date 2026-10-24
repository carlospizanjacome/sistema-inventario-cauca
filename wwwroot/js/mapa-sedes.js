// wwwroot/js/mapa-sedes.js

// Función para cargar Leaflet dinámicamente si no existe
function cargarLeaflet() {
    return new Promise((resolve) => {
        if (window.L) {
            resolve();
            return;
        }

        // Cargar CSS
        if (!document.querySelector('link[href*="leaflet.css"]')) {
            const link = document.createElement('link');
            link.rel = 'stylesheet';
            link.href = 'https://unpkg.com/leaflet@1.9.4/dist/leaflet.css';
            document.head.appendChild(link);
        }

        // Cargar JS
        if (!document.querySelector('script[src*="leaflet.js"]')) {
            const script = document.createElement('script');
            script.src = 'https://unpkg.com/leaflet@1.9.4/dist/leaflet.js';
            script.onload = () => resolve();
            document.head.appendChild(script);
        } else {
            // Si el script ya se está cargando, esperar
            const check = setInterval(() => {
                if (window.L) {
                    clearInterval(check);
                    resolve();
                }
            }, 100);
        }
    });
}

// Función principal que llama Blazor
window.inicializarMapaSedes = async function (dotnetHelper, sedes) {
    await cargarLeaflet();
    await new Promise(r => setTimeout(r, 100));

    // Eliminar mapa previo
    if (window._mapaSedes) {
        window._mapaSedes.remove();
        window._mapaSedes = null;
    }

    const mapa = L.map('mapa-sedes-global').setView([4.5709, -74.2973], 6);
    window._mapaSedes = mapa;

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '© OpenStreetMap'
    }).addTo(mapa);

    const bounds = [];

    sedes.forEach(sede => {
        bounds.push([sede.lat, sede.lng]);

        const popupContent = `
            <div style="min-width: 240px; font-family: 'Segoe UI', sans-serif;">
                <h4 style="margin: 0 0 5px 0; color: #1e3a8a; font-weight: bold;">${sede.nombre}</h4>
                <span style="font-size: 11px; background: #e0e7ff; color: #3730a3; padding: 2px 8px; border-radius: 4px;">${sede.tipo}</span>
                <p style="margin: 8px 0; font-size: 12px; color: #64748b;">📍 ${sede.direccion ?? 'Sin dirección'}</p>
                
                <div style="background: #f8fafc; border-radius: 6px; padding: 10px; margin-top: 10px;">
                    <div style="display: flex; justify-content: space-between; margin-bottom: 6px;">
                        <span style="font-size: 12px;">Bienes Totales:</span>
                        <strong style="font-size: 12px;">${sede.bienes}</strong>
                    </div>
                    <div style="display: flex; justify-content: space-between; margin-bottom: 6px;">
                        <span style="font-size: 12px;">Valor PPE:</span>
                        <strong style="font-size: 12px; color: #059669;">$${sede.ppe.toLocaleString('es-CO')}</strong>
                    </div>
                    <div style="display: flex; justify-content: space-between;">
                        <span style="font-size: 12px;">Stock Consumo:</span>
                        <strong style="font-size: 12px; color: #d97706;">$${sede.consumo.toLocaleString('es-CO')}</strong>
                    </div>
                </div>
            </div>
        `;

        L.marker([sede.lat, sede.lng]).addTo(mapa).bindPopup(popupContent);
    });

    if (bounds.length > 0) {
        mapa.fitBounds(bounds, { padding: [50, 50] });
    }
};