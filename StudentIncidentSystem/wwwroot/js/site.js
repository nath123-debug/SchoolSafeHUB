document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('time.local-time').forEach(function (el) {
        var d = new Date(el.getAttribute('datetime'));
        if (!isNaN(d.getTime())) {
            el.textContent = d.toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' });
        }
    });
});

window.renderChart = function (canvasId, type, labels, values, label) {
    var el = document.getElementById(canvasId);
    if (!el || typeof Chart === 'undefined') return;

    var palette = ['#1f3a68', '#2f6fb5', '#5aa0d8', '#8fc1e8', '#f0ad4e', '#d9534f', '#6c757d', '#20a39e', '#7b61a8'];
    var isBar = type === 'bar';

    new Chart(el, {
        type: type,
        data: {
            labels: labels,
            datasets: [{
                label: label,
                data: values,
                backgroundColor: isBar ? '#1f3a68' : palette,
                borderWidth: isBar ? 0 : 1
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { display: !isBar, position: 'bottom' } },
            scales: isBar ? { y: { beginAtZero: true, ticks: { precision: 0 } } } : {}
        }
    });
};
