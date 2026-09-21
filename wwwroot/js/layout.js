window.almacenLayout = {
    getViewportWidth: () => window.innerWidth,

    getStorageItem: (key) => {
        try { return localStorage.getItem(key); }
        catch { return null; }
    },

    setStorageItem: (key, value) => {
        try { localStorage.setItem(key, value); }
        catch { /* ignorar */ }
    }
};

// Descarga un archivo binario codificado en Base64
window.descargarArchivo = function (base64, nombre, mimeType) {
    try {
        const binario = atob(base64);
        const bytes = new Uint8Array(binario.length);
        for (let i = 0; i < binario.length; i++) {
            bytes[i] = binario.charCodeAt(i);
        }
        const blob = new Blob([bytes], { type: mimeType });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = nombre;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    } catch (e) {
        console.error('Error al descargar archivo:', e);
    }
};
