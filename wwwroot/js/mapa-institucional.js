// wwwroot/js/mapa-institucional.js

window.inicializarMapaInstitucional = async function (dotnetHelper, sedesData) {
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

    if (window._mapaInst) {
        window._mapaInst.remove();
        window._mapaInst = null;
    }

    const mapa = L.map('mapa-institucional', {
        zoomControl: true,
        scrollWheelZoom: true
    }).setView([2.4448, -76.6065], 13);

    window._mapaInst = mapa;

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '© OpenStreetMap',
        maxZoom: 19
    }).addTo(mapa);

    const bounds = [];

    sedesData.forEach(sede => {
        bounds.push([sede.lat, sede.lng]);

        const marker = L.marker([sede.lat, sede.lng]).addTo(mapa);

        marker.bindTooltip(sede.nombre, {
            direction: 'top',
            offset: [0, -35],
            opacity: 0.9,
            className: 'tooltip-sede'
        });

        const formatMoneda = (valor) => new Intl.NumberFormat('es-CO').format(valor || 0);

        const popupContent = `
            <div style="font-family: 'Segoe UI', sans-serif; min-width: 220px;">
                <div style="text-align: center; margin-bottom: 6px;">
                    <strong style="color: #1e3a8a; font-size: 12px;">${sede.nombre}</strong><br>
                    <span style="color: #64748b; font-size: 10px;">📍 ${sede.direccion || 'Sin dirección'}</span>
                </div>
                
                <hr style="margin: 4px 0; border: 0; border-top: 1px solid #e2e8f0;">
                
                <div style="display: flex; justify-content: space-between; margin-bottom: 2px;">
                    <span style="font-size: 10px; color: #475569;">Bienes:</span>
                    <strong style="font-size: 10px; color: #0f172a;">${sede.bienes || 0}</strong>
                </div>
                <div style="display: flex; justify-content: space-between; margin-bottom: 2px;">
                    <span style="font-size: 10px; color: #475569;">Valor PPE:</span>
                    <strong style="font-size: 10px; color: #059669;">$${formatMoneda(sede.ppe)}</strong>
                </div>
                <div style="display: flex; justify-content: space-between; margin-bottom: 2px;">
                    <span style="font-size: 10px; color: #475569;">Stock Consumo:</span>
                    <strong style="font-size: 10px; color: #d97706;">$${formatMoneda(sede.consumo)}</strong>
                </div>
                
                <hr style="margin: 4px 0; border: 0; border-top: 1px dashed #cbd5e1;">
                
                <div style="display: flex; justify-content: space-between; margin-bottom: 2px;">
                    <span style="font-size: 10px; color: #475569;"><i class="bi bi-box-seam" style="color:#3b82f6;"></i> Entradas PPE:</span>
                    <strong style="font-size: 10px; color: #3b82f6;">${sede.entradasPpe || 0}</strong>
                </div>
                <div style="display: flex; justify-content: space-between; margin-bottom: 2px;">
                    <span style="font-size: 10px; color: #475569;"><i class="bi bi-cart-fill" style="color:#10b981;"></i> Entradas Consumo:</span>
                    <strong style="font-size: 10px; color: #10b981;">${sede.entradasConsumo || 0}</strong>
                </div>
                
                <hr style="margin: 4px 0; border: 0; border-top: 1px dashed #cbd5e1;">
                
                <div style="display: flex; justify-content: space-between; margin-bottom: 2px;">
                    <span style="font-size: 10px; color: #475569;"><i class="bi bi-box-arrow-up" style="color:#ef4444;"></i> Salidas PPE:</span>
                    <strong style="font-size: 10px; color: #ef4444;">${sede.salidasPpe || 0}</strong>
                </div>
                <div style="display: flex; justify-content: space-between; margin-bottom: 2px;">
                    <span style="font-size: 10px; color: #475569;"><i class="bi bi-box-arrow-up-right" style="color:#f59e0b;"></i> Salidas Consumo:</span>
                    <strong style="font-size: 10px; color: #f59e0b;">${sede.salidasConsumo || 0}</strong>
                </div>
            </div>
        `;

        // La configuración del popup se queda igual (a la derecha y con autoPan)
        marker.bindPopup(popupContent, {
            maxWidth: 280,
            autoPan: true,
            autoPanPadding: [20, 20],
            offset: [120, 0]
        });

        // ✅ SOLO actualizamos el panel derecho. Quitamos el setView para que no descuadre.
        marker.on('click', () => {
            dotnetHelper.invokeMethodAsync('SeleccionarSedeEnMapa', sede.id);
        });
    });

    if (bounds.length > 0) {
        mapa.fitBounds(bounds, { padding: [50, 50] });
    }
};

// Estilos
const style = document.createElement('style');
style.innerHTML = `
    .tooltip-sede {
        background-color: #1e3a8a !important;
        color: white !important;
        border: none !important;
        border-radius: 6px !important;
        padding: 4px 10px !important;
        font-family: 'Segoe UI', sans-serif !important;
        font-weight: 600 !important;
        font-size: 11px !important;
        box-shadow: 0 4px 6px rgba(0,0,0,0.2) !important;
    }
    .tooltip-sede::before {
        border-top-color: #1e3a8a !important;
    }
    
    .leaflet-popup-content-wrapper {
        border-radius: 8px !important;
        box-shadow: 0 4px 12px rgba(0,0,0,0.2) !important;
    }
    .leaflet-popup-content {
        margin: 10px 14px !important;
    }
    
    .leaflet-popup-tip-container {
        display: none !important;
    }
`;
document.head.appendChild(style);