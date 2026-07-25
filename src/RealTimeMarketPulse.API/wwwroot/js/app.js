document.addEventListener('DOMContentLoaded', () => {
    const tickerGrid = document.getElementById('ticker-grid');
    const feedList = document.getElementById('feed-list');
    const chartTitle = document.getElementById('chart-title');
    const symbolSelect = document.getElementById('chart-symbol-select');
    const providerNameEl = document.getElementById('provider-name');

    // Status elements
    const valSignalr = document.getElementById('val-signalr');
    const dotSignalr = document.querySelector('#pill-signalr .status-dot');
    const valRedis = document.getElementById('val-redis');
    const dotRedis = document.querySelector('#pill-redis .status-dot');
    const valQuartz = document.getElementById('val-quartz');
    const valFetchCount = document.getElementById('val-fetch-count');

    let currentSelectedSymbol = 'BTC';
    const priceHistory = {
        'BTC': [], 'ETH': [], 'SOL': [], 'BNB': [], 'XRP': [], 'DOGE': []
    };
    const maxHistoryPoints = 25;

    // Initialize Chart.js
    const ctx = document.getElementById('liveChart').getContext('2d');
    const chartGradient = ctx.createLinearGradient(0, 0, 0, 220);
    chartGradient.addColorStop(0, 'rgba(6, 182, 212, 0.4)');
    chartGradient.addColorStop(1, 'rgba(6, 182, 212, 0.0)');

    const liveChart = new Chart(ctx, {
        type: 'line',
        data: {
            labels: [],
            datasets: [{
                label: 'Fiyat ($ USD)',
                data: [],
                borderColor: '#06b6d4',
                backgroundColor: chartGradient,
                borderWidth: 2,
                pointRadius: 3,
                pointBackgroundColor: '#fff',
                pointBorderColor: '#06b6d4',
                fill: true,
                tension: 0.3
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { display: false },
                tooltip: {
                    mode: 'index',
                    intersect: false,
                    callbacks: {
                        label: (ctx) => `$${Number(ctx.raw).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 4 })}`
                    }
                }
            },
            scales: {
                x: {
                    grid: { display: false, color: 'rgba(255, 255, 255, 0.05)' },
                    ticks: { color: '#6b7280', font: { size: 10 } }
                },
                y: {
                    grid: { color: 'rgba(255, 255, 255, 0.05)' },
                    ticks: {
                        color: '#9ca3af',
                        font: { family: 'JetBrains Mono', size: 10 },
                        callback: (value) => `$${value.toLocaleString()}`
                    }
                }
            },
            animation: { duration: 400 }
        }
    });

    const signalRModule = window.signalR || window.signalr || signalR;
    const connection = new signalRModule.HubConnectionBuilder()
        .withUrl('/hub/market')
        .withAutomaticReconnect()
        .build();

    connection.on('ReceiveMarketTickers', (tickers) => {
        updateTickerGrid(tickers);
        updateChartWithTickers(tickers);
    });

    connection.on('ReceiveSymbolUpdate', (ticker) => {
        updateSingleTickerCard(ticker);
        if (ticker.symbol === currentSelectedSymbol) {
            appendChartData(ticker.priceUsd, new Date(ticker.timestamp).toLocaleTimeString());
        }
    });

    connection.on('ReceiveMarketAlert', (alert) => {
        addFeedItem(alert.title, alert.message, alert.severity);
    });

    connection.on('ReceiveSystemStatus', (statusJson) => {
        try {
            const status = JSON.parse(statusJson);
            updateStatusHeader(status);
        } catch (e) { console.error('Status parse error', e); }
    });

    async function startSignalR() {
        try {
            valSignalr.textContent = 'Bağlanıyor...';
            await connection.start();
            valSignalr.textContent = 'Bağlandı (Aktif)';
            dotSignalr.className = 'status-dot dot-green';
            addFeedItem('WebSocket Bağlantısı', 'SignalR sunucusuyla anlık canlı veri akışı başlatıldı.', 'info');
        } catch (err) {
            valSignalr.textContent = 'Bağlantı Hatası';
            dotSignalr.className = 'status-dot dot-red';
            setTimeout(startSignalR, 4000);
        }
    }

    connection.onreconnecting(() => {
        valSignalr.textContent = 'Yeniden Bağlanıyor...';
        dotSignalr.className = 'status-dot dot-amber';
    });

    connection.onreconnected(() => {
        valSignalr.textContent = 'Bağlandı (Aktif)';
        dotSignalr.className = 'status-dot dot-green';
    });

    // Fetch initial status via REST
    fetch('/api/market/status')
        .then(res => res.json())
        .then(data => updateStatusHeader(data))
        .catch(() => {});

    startSignalR();

    // DOM Update Functions
    function updateStatusHeader(status) {
        const cacheMode = status.cacheMode || status.CacheMode || (status.isRedisConnected || status.IsRedisConnected ? 'Redis (Pub/Sub)' : 'In-Memory (Fallback)');
        const isRedis = status.isRedisConnected !== undefined ? status.isRedisConnected : status.IsRedisConnected;
        const quartzStatus = status.quartzStatus || status.QuartzStatus || 'Aktif';
        const refreshInterval = status.refreshIntervalSeconds || status.RefreshIntervalSeconds || 3;
        const totalFetchCount = status.totalFetchCount !== undefined ? status.totalFetchCount : (status.TotalFetchCount !== undefined ? status.TotalFetchCount : 0);
        const activeProvider = status.activeProvider || status.ActiveProvider;

        valRedis.textContent = cacheMode;
        dotRedis.className = `status-dot ${isRedis ? 'dot-green' : 'dot-amber'}`;
        valQuartz.textContent = `${quartzStatus} (${refreshInterval}s)`;
        valFetchCount.textContent = `#${totalFetchCount}`;
        if (activeProvider) {
            providerNameEl.textContent = activeProvider;
        }
    }

    function updateTickerGrid(tickers) {
        if (!tickers || !tickers.length) return;
        tickerGrid.innerHTML = '';
        tickers.forEach(t => {
            const card = createTickerCardElement(t);
            tickerGrid.appendChild(card);
        });
        if (typeof lucide !== 'undefined' && lucide.createIcons) {
            lucide.createIcons();
        }
    }

    function createTickerCardElement(t) {
        const div = document.createElement('div');
        div.className = 'ticker-card';
        div.id = `card-${t.symbol}`;
        div.onclick = () => selectSymbol(t.symbol);

        const isUp = t.changePercent24Hr >= 0;
        const changeClass = isUp ? 'change-up' : 'change-down';
        const changeSign = isUp ? '+' : '';
        const priceFormatted = Number(t.priceUsd).toLocaleString('en-US', { minimumFractionDigits: t.priceUsd < 1 ? 4 : 2, maximumFractionDigits: t.priceUsd < 1 ? 4 : 2 });
        const highFormatted = Number(t.high24Hr).toLocaleString('en-US', { maximumFractionDigits: 2 });
        const lowFormatted = Number(t.low24Hr).toLocaleString('en-US', { maximumFractionDigits: 2 });

        div.innerHTML = `
            <div class="ticker-top">
                <div class="ticker-ident">
                    <span class="ticker-symbol">${t.symbol}</span>
                    <span class="ticker-name">${t.name}</span>
                </div>
                <span class="ticker-change ${changeClass}">${changeSign}${t.changePercent24Hr.toFixed(2)}%</span>
            </div>
            <div class="ticker-price">$${priceFormatted}</div>
            <div class="ticker-stats">
                <span>En Yüksek: $${highFormatted}</span>
                <span>En Düşük: $${lowFormatted}</span>
            </div>
        `;
        return div;
    }

    function updateSingleTickerCard(t) {
        const card = document.getElementById(`card-${t.symbol}`);
        if (!card) return;

        const priceEl = card.querySelector('.ticker-price');
        const changeEl = card.querySelector('.ticker-change');
        const oldPrice = parseFloat(priceEl.textContent.replace('$', '').replace(/,/g, ''));
        
        const isUp = t.changePercent24Hr >= 0;
        changeEl.className = `ticker-change ${isUp ? 'change-up' : 'change-down'}`;
        changeEl.textContent = `${isUp ? '+' : ''}${t.changePercent24Hr.toFixed(2)}%`;
        priceEl.textContent = `$${Number(t.priceUsd).toLocaleString('en-US', { minimumFractionDigits: t.priceUsd < 1 ? 4 : 2, maximumFractionDigits: t.priceUsd < 1 ? 4 : 2 })}`;

        if (t.priceUsd > oldPrice) {
            card.classList.remove('flash-up', 'flash-down');
            void card.offsetWidth;
            card.classList.add('flash-up');
        } else if (t.priceUsd < oldPrice) {
            card.classList.remove('flash-up', 'flash-down');
            void card.offsetWidth;
            card.classList.add('flash-down');
        }
    }

    function selectSymbol(symbol) {
        currentSelectedSymbol = symbol;
        symbolSelect.value = symbol;
        chartTitle.textContent = `${symbol} / USD Canlı Takip`;
        renderChartForSymbol(symbol);
    }

    symbolSelect.addEventListener('change', (e) => {
        selectSymbol(e.target.value);
    });

    function updateChartWithTickers(tickers) {
        const timeStr = new Date().toLocaleTimeString();
        tickers.forEach(t => {
            if (!priceHistory[t.symbol]) priceHistory[t.symbol] = [];
            priceHistory[t.symbol].push({ time: timeStr, price: t.priceUsd });
            if (priceHistory[t.symbol].length > maxHistoryPoints) {
                priceHistory[t.symbol].shift();
            }
        });
        renderChartForSymbol(currentSelectedSymbol);
    }

    function appendChartData(price, timeStr) {
        if (!priceHistory[currentSelectedSymbol]) priceHistory[currentSelectedSymbol] = [];
        priceHistory[currentSelectedSymbol].push({ time: timeStr, price: price });
        if (priceHistory[currentSelectedSymbol].length > maxHistoryPoints) {
            priceHistory[currentSelectedSymbol].shift();
        }
        renderChartForSymbol(currentSelectedSymbol);
    }

    function renderChartForSymbol(symbol) {
        const dataPoints = priceHistory[symbol] || [];
        liveChart.data.labels = dataPoints.map(p => p.time);
        liveChart.data.datasets[0].data = dataPoints.map(p => p.price);
        liveChart.update();
    }

    function addFeedItem(title, msg, severity) {
        const item = document.createElement('div');
        item.className = `feed-item item-${severity || 'info'}`;
        const timeStr = new Date().toLocaleTimeString();
        item.innerHTML = `
            <div class="feed-time">${timeStr} &bull; ${title}</div>
            <div class="feed-content">${msg}</div>
        `;
        feedList.insertBefore(item, feedList.firstChild);
        if (feedList.children.length > 20) {
            feedList.removeChild(feedList.lastChild);
        }
    }

    // Button Controls
    document.getElementById('btn-force-fetch').addEventListener('click', async () => {
        try {
            const res = await fetch('/api/market/trigger', { method: 'POST' });
            const data = await res.json();
            addFeedItem('Manuel Tetikleme', data.message, 'success');
        } catch (err) {
            addFeedItem('Hata', 'Arka plan işi tetiklenirken hata oluştu.', 'danger');
        }
    });

    document.getElementById('btn-test-alert').addEventListener('click', async () => {
        try {
            const res = await fetch('/api/market/test-alert', { method: 'POST' });
            const data = await res.json();
            addFeedItem('Test Uyarı Gönderimi', data.message, 'warning');
        } catch (err) {
            addFeedItem('Hata', 'Test uyarısı gönderilirken hata oluştu.', 'danger');
        }
    });
});
