// Stub dashboard - evita error que rompe circuito Blazor
window.dashboardCharts = window.dashboardCharts || {
    crearBarCategorias: function () { },
    crearDoughnut: function () { },
    crearLinea: function () { },
    crearBarra: function () { },
    crearBar: function () { },
    crearPie: function () { },
    crearGrafico: function () { },
    init: function () { },
    inicializar: function () { }
};

console.log('✅ cd-charts.js cargado');

window.CDCharts = {
    _charts: {},

    _destruir: function (id) {
        if (this._charts[id]) {
            try { this._charts[id].destroy(); } catch (e) { }
            delete this._charts[id];
        }
    },

    _canvas: function (id) {
        const c = document.getElementById(id);
        if (!c) { console.warn('⚠ No existe canvas:', id); return null; }
        if (typeof Chart === 'undefined') { console.error('❌ Chart.js no cargado'); return null; }

        // ✅ Destruir cualquier chart existente asociado a este canvas
        const existente = Chart.getChart(c);
        if (existente) {
            try { existente.destroy(); } catch (e) { }
        }

        return c;
    },

    // ═══ DOUGHNUT ═══
    crearDoughnut: function (id, labels, data) {
        console.log('🎨 crearDoughnut:', id);
        const c = this._canvas(id); if (!c) return false;
        this._destruir(id);

        try {
            this._charts[id] = new Chart(c, {
                type: 'doughnut',
                data: {
                    labels: labels,
                    datasets: [{
                        data: data,
                        backgroundColor: ['#4f46e5', '#10b981', '#f59e0b', '#ef4444', '#8b5cf6'],
                        borderWidth: 2,
                        borderColor: '#fff'
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: {
                        legend: { position: 'bottom', labels: { padding: 16, font: { size: 12 } } },
                        tooltip: {
                            callbacks: {
                                label: function (ctx) {
                                    const val = ctx.parsed || 0;
                                    const total = ctx.dataset.data.reduce((a, b) => a + b, 0);
                                    const pct = total > 0 ? ((val / total) * 100).toFixed(1) : 0;
                                    return ctx.label + ': ' + val.toLocaleString('es-CO') + ' (' + pct + '%)';
                                }
                            }
                        }
                    }
                }
            });
            console.log('✅ Doughnut creado:', id);
            return true;
        } catch (e) {
            console.error('❌ Error doughnut:', e);
            return false;
        }
    },

    // ═══ BARRAS HORIZONTALES ═══
    crearBarrasHorizontales: function (id, labels, data) {
        console.log('🎨 crearBarrasHorizontales:', id);
        const c = this._canvas(id); if (!c) return false;
        this._destruir(id);

        try {
            this._charts[id] = new Chart(c, {
                type: 'bar',
                data: {
                    labels: labels,
                    datasets: [{
                        label: 'Cantidad',
                        data: data,
                        backgroundColor: '#4f46e5',
                        borderRadius: 6,
                        barThickness: 24
                    }]
                },
                options: {
                    indexAxis: 'y',
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: { legend: { display: false } },
                    scales: {
                        x: { beginAtZero: true, ticks: { stepSize: 1, font: { size: 11 } } },
                        y: { ticks: { font: { size: 11 } } }
                    }
                }
            });
            console.log('✅ Barras horizontales creadas:', id);
            return true;
        } catch (e) {
            console.error('❌ Error barras horiz:', e);
            return false;
        }
    },

    // ═══ LÍNEA ═══
    crearLinea: function (id, labels, data) {
        console.log('🎨 crearLinea:', id);
        const c = this._canvas(id); if (!c) return false;
        this._destruir(id);

        try {
            this._charts[id] = new Chart(c, {
                type: 'line',
                data: {
                    labels: labels,
                    datasets: [{
                        label: 'Valor entradas',
                        data: data,
                        borderColor: '#4f46e5',
                        backgroundColor: 'rgba(79, 70, 229, 0.1)',
                        fill: true,
                        tension: 0.3,
                        pointBackgroundColor: '#4f46e5',
                        pointRadius: 5,
                        pointHoverRadius: 7
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: { legend: { display: false } },
                    scales: { y: { beginAtZero: true, ticks: { font: { size: 11 } } } }
                }
            });
            console.log('✅ Línea creada:', id);
            return true;
        } catch (e) {
            console.error('❌ Error línea:', e);
            return false;
        }
    },

    // ═══ BARRAS VERTICALES ═══
    crearBarrasVerticales: function (id, labels, data) {
        console.log('🎨 crearBarrasVerticales:', id);
        const c = this._canvas(id); if (!c) return false;
        this._destruir(id);

        try {
            this._charts[id] = new Chart(c, {
                type: 'bar',
                data: {
                    labels: labels,
                    datasets: [{
                        label: 'Valor',
                        data: data,
                        backgroundColor: ['#4f46e5', '#10b981', '#f59e0b', '#ef4444', '#8b5cf6'],
                        borderRadius: 6
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: { legend: { display: false } },
                    scales: {
                        y: { beginAtZero: true, ticks: { font: { size: 11 } } },
                        x: { ticks: { font: { size: 11 } } }
                    }
                }
            });
            console.log('✅ Barras verticales creadas:', id);
            return true;
        } catch (e) {
            console.error('❌ Error barras vert:', e);
            return false;
        }
    },

    // ═══ BARRAS COMPARATIVAS ═══
    crearBarrasComparativas: function (id, labels, stock, minimo) {
        console.log('🎨 crearBarrasComparativas:', id);
        const c = this._canvas(id); if (!c) return false;
        this._destruir(id);

        try {
            this._charts[id] = new Chart(c, {
                type: 'bar',
                data: {
                    labels: labels,
                    datasets: [
                        {
                            label: 'Stock actual',
                            data: stock,
                            backgroundColor: '#10b981',
                            borderRadius: 6,
                            barThickness: 20
                        },
                        {
                            label: 'Stock mínimo',
                            data: minimo,
                            backgroundColor: '#ef4444',
                            borderRadius: 6,
                            barThickness: 20
                        }
                    ]
                },
                options: {
                    indexAxis: 'y',
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: {
                        legend: { position: 'bottom', labels: { padding: 12, font: { size: 11 } } }
                    },
                    scales: {
                        x: { beginAtZero: true, ticks: { font: { size: 11 } } },
                        y: { ticks: { font: { size: 11 } } }
                    }
                }
            });
            console.log('✅ Barras comparativas creadas:', id);
            return true;
        } catch (e) {
            console.error('❌ Error barras comp:', e);
            return false;
        }
    }
};

console.log('✅ window.CDCharts registrado');