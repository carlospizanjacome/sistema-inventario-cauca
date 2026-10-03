// ═══════════════════════════════════════════════════════════
// Dashboard Charts — Chart.js 4.4
// ═══════════════════════════════════════════════════════════

window.dashboardCharts = {

    // Paleta profesional
    palette: {
        indigo: '#4f46e5',
        indigoLight: 'rgba(79, 70, 229, 0.15)',
        blue: '#3b82f6',
        blueLight: 'rgba(59, 130, 246, 0.15)',
        green: '#10b981',
        orange: '#f97316',
        red: '#ef4444',
        gray: '#94a3b8'
    },

    // Configuración común de opciones
    commonOptions() {
        return {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    display: false
                },
                tooltip: {
                    backgroundColor: '#0f172a',
                    titleColor: '#ffffff',
                    bodyColor: '#cbd5e1',
                    padding: 12,
                    cornerRadius: 8,
                    titleFont: { size: 13, weight: '600' },
                    bodyFont: { size: 12 }
                }
            }
        };
    },

    // ═══════════════════════════════════════════════════════
    // 1. BAR CHART — Bienes por categoría
    // ═══════════════════════════════════════════════════════
    crearBarCategorias(canvasId, labels, data, valores) {
        const ctx = document.getElementById(canvasId);
        if (!ctx) return;

        if (window._barCategorias) window._barCategorias.destroy();

        window._barCategorias = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [{
                    label: 'Bienes',
                    data: data,
                    backgroundColor: this.palette.indigo,
                    hoverBackgroundColor: '#4338ca',
                    borderRadius: 6,
                    borderSkipped: false,
                    barThickness: 24
                }]
            },
            options: {
                ...this.commonOptions(),
                plugins: {
                    ...this.commonOptions().plugins,
                    tooltip: {
                        ...this.commonOptions().plugins.tooltip,
                        callbacks: {
                            label: (context) => {
                                const i = context.dataIndex;
                                const valor = valores[i] || 0;
                                return [
                                    ` Cantidad: ${context.parsed.y} bienes`,
                                    ` Valor: ${this.formatMoneda(valor)}`
                                ];
                            }
                        }
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        grid: { color: '#f1f5f9', drawBorder: false },
                        ticks: { color: '#64748b', font: { size: 11 }, precision: 0 }
                    },
                    x: {
                        grid: { display: false },
                        ticks: {
                            color: '#475569',
                            font: { size: 11, weight: '500' },
                            maxRotation: 45,
                            minRotation: 0
                        }
                    }
                }
            }
        });
    },

    // ═══════════════════════════════════════════════════════
    // 2. DONUT CHART — Distribución por tipo
    // ═══════════════════════════════════════════════════════
    crearDonutTipo(canvasId, devolutivos, consumibles) {
        const ctx = document.getElementById(canvasId);
        if (!ctx) return;

        if (window._donutTipo) window._donutTipo.destroy();

        window._donutTipo = new Chart(ctx, {
            type: 'doughnut',
            data: {
                labels: ['Devolutivos', 'Consumibles'],
                datasets: [{
                    data: [devolutivos, consumibles],
                    backgroundColor: [this.palette.indigo, this.palette.blue],
                    hoverBackgroundColor: ['#4338ca', '#2563eb'],
                    borderColor: '#ffffff',
                    borderWidth: 3,
                    hoverOffset: 8
                }]
            },
            options: {
                ...this.commonOptions(),
                cutout: '70%',
                plugins: {
                    ...this.commonOptions().plugins,
                    legend: {
                        display: true,
                        position: 'bottom',
                        labels: {
                            color: '#475569',
                            padding: 16,
                            font: { size: 12, weight: '500' },
                            usePointStyle: true,
                            pointStyle: 'circle',
                            boxWidth: 8
                        }
                    },
                    tooltip: {
                        ...this.commonOptions().plugins.tooltip,
                        callbacks: {
                            label: (context) => {
                                const total = devolutivos + consumibles;
                                const pct = total > 0 ? ((context.parsed / total) * 100).toFixed(1) : 0;
                                return ` ${context.label}: ${context.parsed} (${pct}%)`;
                            }
                        }
                    }
                }
            }
        });
    },

    // ═══════════════════════════════════════════════════════
    // 3. LINE CHART — Depreciación mensual
    // ═══════════════════════════════════════════════════════
    crearLineDepreciacion(canvasId, labels, depMes, depAcum) {
        const ctx = document.getElementById(canvasId);
        if (!ctx) return;

        if (window._lineDepreciacion) window._lineDepreciacion.destroy();

        const gradient = ctx.getContext('2d').createLinearGradient(0, 0, 0, 280);
        gradient.addColorStop(0, 'rgba(79, 70, 229, 0.25)');
        gradient.addColorStop(1, 'rgba(79, 70, 229, 0.02)');

        window._lineDepreciacion = new Chart(ctx, {
            type: 'line',
            data: {
                labels: labels,
                datasets: [
                    {
                        label: 'Depreciación del mes',
                        data: depMes,
                        borderColor: this.palette.indigo,
                        backgroundColor: gradient,
                        fill: true,
                        tension: 0.4,
                        borderWidth: 2.5,
                        pointRadius: 0,
                        pointHoverRadius: 5,
                        pointHoverBackgroundColor: this.palette.indigo,
                        pointHoverBorderColor: '#ffffff',
                        pointHoverBorderWidth: 2
                    },
                    {
                        label: 'Depreciación acumulada',
                        data: depAcum,
                        borderColor: this.palette.blue,
                        backgroundColor: 'transparent',
                        borderDash: [5, 5],
                        tension: 0.4,
                        borderWidth: 2,
                        pointRadius: 0,
                        pointHoverRadius: 5,
                        pointHoverBackgroundColor: this.palette.blue,
                        pointHoverBorderColor: '#ffffff',
                        pointHoverBorderWidth: 2
                    }
                ]
            },
            options: {
                ...this.commonOptions(),
                interaction: { mode: 'index', intersect: false },
                plugins: {
                    ...this.commonOptions().plugins,
                    legend: {
                        display: true,
                        position: 'top',
                        align: 'end',
                        labels: {
                            color: '#475569',
                            padding: 14,
                            font: { size: 11, weight: '500' },
                            usePointStyle: true,
                            pointStyle: 'circle',
                            boxWidth: 8
                        }
                    },
                    tooltip: {
                        ...this.commonOptions().plugins.tooltip,
                        callbacks: {
                            label: (context) => ` ${context.dataset.label}: ${this.formatMoneda(context.parsed.y)}`
                        }
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        grid: { color: '#f1f5f9', drawBorder: false },
                        ticks: {
                            color: '#64748b',
                            font: { size: 11 },
                            callback: (v) => this.formatMonedaCorto(v)
                        }
                    },
                    x: {
                        grid: { display: false },
                        ticks: { color: '#64748b', font: { size: 11 } }
                    }
                }
            }
        });
    },

    // ═══════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════
    formatMoneda(valor) {
        return new Intl.NumberFormat('es-CO', {
            style: 'currency',
            currency: 'COP',
            minimumFractionDigits: 0,
            maximumFractionDigits: 0
        }).format(valor);
    },

    formatMonedaCorto(valor) {
        if (valor >= 1_000_000_000) return '$' + (valor / 1_000_000_000).toFixed(1) + 'B';
        if (valor >= 1_000_000) return '$' + (valor / 1_000_000).toFixed(1) + 'M';
        if (valor >= 1_000) return '$' + (valor / 1_000).toFixed(0) + 'K';
        return '$' + valor;
    }
};