// Unifi Camera Dashboard JavaScript

// Initialize camera streams
window.initializeCameraStreams = function (cameras) {
    console.log('Initializing camera streams:', cameras);

    cameras.forEach(camera => {
        const imgElement = document.getElementById(`camera-${camera.id}`);
        if (imgElement) {
            // Auto-refresh snapshot every 5 seconds
            setInterval(() => {
                const timestamp = new Date().getTime();
                imgElement.src = `${camera.snapshotUrl}?t=${timestamp}`;
            }, 5000);
        }
    });
};

// Fullscreen handling
window.toggleFullscreen = async function () {
    if (!document.fullscreenElement) {
        try {
            await document.documentElement.requestFullscreen();
        } catch (err) {
            console.error('Error enabling fullscreen mode:', err);
        }
    } else {
        if (document.exitFullscreen) {
            document.exitFullscreen();
        }
    }
};

// Show single camera in fullscreen
window.showCameraFullscreen = function (cameraId) {
    const imgElement = document.getElementById(`camera-${cameraId}`);
    if (imgElement && imgElement.requestFullscreen) {
        imgElement.requestFullscreen();
    }
};

// Wake Lock API - prevents tablet from going to standby
let wakeLock = null;

window.requestWakeLock = async function () {
    if ('wakeLock' in navigator) {
        try {
            wakeLock = await navigator.wakeLock.request('screen');
            console.log('Wake lock activated - screen stays on');

            // Restore wake lock on visibility change
            document.addEventListener('visibilitychange', async () => {
                if (wakeLock !== null && document.visibilityState === 'visible') {
                    wakeLock = await navigator.wakeLock.request('screen');
                }
            });
        } catch (err) {
            console.error('Wake lock could not be activated:', err);
        }
    } else {
        console.warn('Wake Lock API not available in this browser');
    }
};

// Keep-alive function - prevents session timeout
window.setupKeepAlive = function () {
    // SignalR keep-alive via regular ping requests
    setInterval(() => {
        // Small invisible request to keep connection active
        fetch('/api/ping', { method: 'HEAD' }).catch(() => {
            console.log('Keep-alive ping failed');
        });
    }, 60000); // every 60 seconds
};

// Auto-reconnect on connection loss
window.setupAutoReconnect = function () {
    let reconnectAttempts = 0;
    const maxReconnectAttempts = 10;

    window.addEventListener('offline', () => {
        console.log('Connection lost - attempting reconnect...');
        attemptReconnect();
    });

    function attemptReconnect() {
        if (reconnectAttempts >= maxReconnectAttempts) {
            console.error('Max reconnect attempts reached');
            return;
        }

        reconnectAttempts++;
        console.log(`Reconnect attempt ${reconnectAttempts}/${maxReconnectAttempts}`);

        setTimeout(() => {
            fetch('/api/ping', { method: 'HEAD' })
                .then(() => {
                    console.log('Connection restored');
                    reconnectAttempts = 0;
                    location.reload(); // reload page after successful reconnection
                })
                .catch(() => {
                    attemptReconnect(); // try again
                });
        }, 5000 * reconnectAttempts); // exponential backoff
    }
};

// Keyboard Shortcuts
document.addEventListener('keydown', (e) => {
    // F11 - Fullscreen Toggle
    if (e.key === 'F11') {
        e.preventDefault();
        window.toggleFullscreen();
    }

    // R - Refresh
    if (e.key === 'r' || e.key === 'R') {
        if (e.ctrlKey || e.metaKey) {
            return; // standard browser refresh
        }
    }
});

// Initialize everything when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    console.log('Unifi Camera Dashboard loaded');
    window.requestWakeLock();
    window.setupKeepAlive();
    window.setupAutoReconnect();
});

// Visibility Change Handler - re-acquires the wake lock
document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'visible') {
        console.log('Tab visible again - reactivating wake lock');
        window.requestWakeLock();
    }
});
