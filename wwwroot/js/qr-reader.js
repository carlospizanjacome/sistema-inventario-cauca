// ═══════════════════════════════════════════════════════════
// QR Reader — Usa html5-qrcode (cargado desde CDN)
// ═══════════════════════════════════════════════════════════

window.almacenQrReader = {
    _scanner: null,
    _dotNetRef: null,

    /**
     * Inicia el lector de QR usando la cámara del dispositivo.
     * @param {string} elementId — ID del div donde se renderiza el video
     * @param {object} dotNetRef — referencia .NET para callback
     */
    start: async function (elementId, dotNetRef) {
        try {
            // Cargar librería si no está
            if (typeof Html5Qrcode === 'undefined') {
                await this._loadScript('https://unpkg.com/html5-qrcode@2.3.8/html5-qrcode.min.js');
            }

            this._dotNetRef = dotNetRef;

            const config = {
                fps: 10,
                qrbox: { width: 250, height: 250 },
                aspectRatio: 1.0
            };

            this._scanner = new Html5Qrcode(elementId);

            await this._scanner.start(
                { facingMode: 'environment' },  // cámara trasera en móvil
                config,
                (decodedText) => this._onScanSuccess(decodedText),
                (errorMessage) => { /* ignorar errores de frame */ }
            );

            return true;
        } catch (err) {
            console.error('Error al iniciar QR reader:', err);
            return false;
        }
    },

    stop: async function () {
        if (this._scanner) {
            try {
                await this._scanner.stop();
                this._scanner.clear();
            } catch (e) { /* ignorar */ }
            this._scanner = null;
        }
    },

    _onScanSuccess: function (decodedText) {
        if (this._dotNetRef) {
            this._dotNetRef.invokeMethodAsync('OnQrEscaneado', decodedText);
        }
    },

    _loadScript: function (src) {
        return new Promise((resolve, reject) => {
            const script = document.createElement('script');
            script.src = src;
            script.onload = resolve;
            script.onerror = reject;
            document.head.appendChild(script);
        });
    }
};