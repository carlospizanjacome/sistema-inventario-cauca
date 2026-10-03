// ═══════════════════════════════════════════════════════════
// Dashboard Charts — Chart.js 4.4
// Paleta y estilo: indigo/slate profesional
// ═══════════════════════════════════════════════════════════

window.dashboardCharts = {

    palette: {
        indigo: '#4f46e5',
        indigoSoft: '#818cf8',
        blue: '#3b82f6',
        cyan: '#06b6d4',
        slate: '#94a3b8',
        darkBg: '#0f172a'
    },

    tooltipConfig() {
        return {
            padding: 12,
            backgroundColor: '#0f172a',
            titleColor: '#ffffff',
            bodyColor: '#cbd5e1',
            titleFont: { size: 13, weight: '700' },
            bodyFont: { size: 12 },
            cornerRadius: 8,
            borderColor: 'transparent',
            displayColors: false
        };
    },

    // ═══════════════════════════════════════════════════════
    // 1. BAR CHART — Bienes por categoría
    // ═══════════════════════════════════════════════════════
    crearBarCategorias(canvasId, labels, data) {
        const ctx = document.getElementById(canvasId);
        if (!ctx) return;

        if (window._barCategorias) window._barCategorias.destroy();

        window._barCategorias = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [{
                    label: 'Cantidad de Activos',
                    data: data,
                    backgroundColor: this.palette.indigo,
                    hoverBackgroundColor: '#4338ca',
                    borderRadius: 8,
                    barThickness: 32,
                    maxBarThickness: 40
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        ...this.tooltipConfig(),
                        callbacks: {
                            label: (context) => ` ${context.parsed.y} activo${context.parsed.y !== 1 ? 's' : ''}`
                        }
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        ticks: {
                            stepSize: 1,
                            color: '#94a3b8',
                            font: { size: 11, weight: '500' }
                        },
                        grid: {
                            color: '#f1f5f9',
                            drawBorder: false
                        }
                    },
                    x: {
                        ticks: {
                            color: '#64748b',
                            font: { size: 11, weight: '600' }
                        },
                        grid: { display: false }
                    }
                }
            }
        });
    },

    // ═══════════════════════════════════════════════════════
    // 2. DONUT — Distribución por tipo
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
                    backgroundColor: ['#3b82f6', '#06b6d4'],
                    borderWidth: 4,
                    borderColor: '#ffffff',
                    hoverOffset: 6
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                cutout: '72%',
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: {
                            boxWidth: 12,
                            boxHeight: 12,
                            padding: 20,
                            color: '#475569',
                            font: { size: 12, weight: '600' },
                            usePointStyle: true,
                            pointStyle: 'circle'
                        }
                    },
                    tooltip: {
                        ...this.tooltipConfig(),
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
    // 3. LINE — Depreciación mensual
    // ═══════════════════════════════════════════════════════
    crearLineDepreciacion(canvasId, labels, depMes, depAcum) {
        const ctx = document.getElementById(canvasId);
        if (!ctx) return;

        if (window._lineDep) window._lineDep.destroy();

        window._lineDep = new Chart(ctx, {
            type: 'line',
            data: {
                labels: labels,
                datasets: [
                    {
                        label: 'Depreciación del mes',
                        data: depMes,
                        borderColor: '#4f46e5',
                        borderWidth: 3,
                        pointRadius: 0,
                        pointHoverRadius: 6,
                        pointHoverBackgroundColor: '#4f46e5',
                        pointHoverBorderColor: '#ffffff',
                        pointHoverBorderWidth: 2,
                        tension: 0.15,
                        fill: false
                    },
                    {
                        label: 'Depreciación acumulada',
                        data: depAcum,
                        borderColor: '#94a3b8',
                        borderWidth: 2,
                        borderDash: [6, 5],
                        pointRadius: 0,
                        pointHoverRadius: 6,
                        pointHoverBackgroundColor: '#94a3b8',
                        pointHoverBorderColor: '#ffffff',
                        pointHoverBorderWidth: 2,
                        tension: 0.1,
                        fill: false
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: {
                    mode: 'index',
                    intersect: false
                },
                plugins: {
                    legend: {
                        position: 'top',
                        align: 'end',
                        labels: {
                            boxWidth: 12,
                            boxHeight: 12,
                            padding: 14,
                            color: '#475569',
                            font: { size: 11.5, weight: '600' },
                            usePointStyle: true,
                            pointStyle: 'circle'
                        }
                    },
                    tooltip: {
                        ...this.tooltipConfig(),
                        callbacks: {
                            label: (context) => ` ${context.dataset.label}: ${this.formatMoneda(context.parsed.y)}`
                        }
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        ticks: {
                            color: '#94a3b8',
                            font: { size: 11 },
                            callback: (value) => this.formatCorto(value)
                        },
                        grid: {
                            color: '#f1f5f9',
                            drawBorder: false
                        }
                    },
                    x: {
                        ticks: {
                            color: '#94a3b8',
                            font: { size: 11, weight: '500' }
                        },
                        grid: { display: false }
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

    formatCorto(valor) {
        if (valor >= 1_000_000_000) return '$' + (valor / 1_000_000_000).toFixed(1) + 'B';
        if (valor >= 1_000_000) return '$' + (valor / 1_000_000).toFixed(1) + 'M';
        if (valor >= 1_000) return '$' + (valor / 1_000).toFixed(0) + 'K';
        return '$' + valor;
    }
};