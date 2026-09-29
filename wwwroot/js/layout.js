window.descargarArchivo = function (base64, nombre, mimeType) {
    try {
        // Convertir base64 a bytes
        const binario = atob(base64);
        const bytes = new Uint8Array(binario.length);
        for (let i = 0; i < binario.length; i++) {
            bytes[i] = binario.charCodeAt(i);
        }

        // Crear Blob (sin límite de tamaño)
        const blob = new Blob([bytes], { type: mimeType });

        // Crear URL temporal
        const url = URL.createObjectURL(blob);

        // Crear link de descarga
        const link = document.createElement('a');
        link.href = url;
        link.download = nombre;
        link.style.display = 'none';
        document.body.appendChild(link);
        link.click();

        // Limpiar
        setTimeout(() => {
            document.body.removeChild(link);
            URL.revokeObjectURL(url);
        }, 100);

        return true;
    } catch (error) {
        console.error('Error al descargar archivo:', error);
        return false;
    }
};