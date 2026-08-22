// Web Push subscribe/unsubscribe flow for the Notifications section in Settings.razor. Adapted
// from NotifyHub's own demo (github.com/lukislp/NotifyHub, samples/NotifyHub.Demo/wwwroot/
// index.html), pointed at this app's own Controllers/PushController.cs endpoints instead of
// NotifyHub.AspNetCore's (which this app doesn't reference - see the plan/PR description for why).

if ('serviceWorker' in navigator) {
    navigator.serviceWorker.register('/service-worker.js');
}

function urlBase64ToUint8Array(base64String) {
    const padding = '='.repeat((4 - base64String.length % 4) % 4);
    const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
    const rawData = window.atob(base64);
    return Uint8Array.from([...rawData].map(c => c.charCodeAt(0)));
}

window.webPush = {
    isSupported: function () {
        return 'serviceWorker' in navigator && 'PushManager' in window;
    },

    getStatus: async function () {
        if (!window.webPush.isSupported()) return 'unsupported';
        const reg = await navigator.serviceWorker.ready;
        const sub = await reg.pushManager.getSubscription();
        return sub ? 'enabled' : 'disabled';
    },

    enable: async function () {
        if (!window.webPush.isSupported()) {
            return { success: false, error: 'Push is not supported by this browser.' };
        }

        const permission = await Notification.requestPermission();
        if (permission !== 'granted') {
            return { success: false, error: 'Notification permission was not granted.' };
        }

        try {
            const reg = await navigator.serviceWorker.ready;
            let sub = await reg.pushManager.getSubscription();
            if (!sub) {
                const keyResp = await fetch('/api/push/vapid-public-key');
                const { publicKey } = await keyResp.json();
                sub = await reg.pushManager.subscribe({
                    userVisibleOnly: true,
                    applicationServerKey: urlBase64ToUint8Array(publicKey),
                });
            }

            const json = sub.toJSON();
            const resp = await fetch('/api/push/subscribe', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    endpoint: sub.endpoint,
                    p256dh: json.keys.p256dh,
                    auth: json.keys.auth,
                }),
            });

            if (!resp.ok) {
                return { success: false, error: 'Failed to register the subscription with the server.' };
            }

            return { success: true };
        } catch (err) {
            return { success: false, error: err.message || String(err) };
        }
    },

    disable: async function () {
        if (!window.webPush.isSupported()) return { success: true };

        const reg = await navigator.serviceWorker.ready;
        const sub = await reg.pushManager.getSubscription();
        if (sub) {
            await fetch('/api/push/unsubscribe', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ endpoint: sub.endpoint }),
            });
            await sub.unsubscribe();
        }

        return { success: true };
    },

    test: async function () {
        try {
            const resp = await fetch('/api/push/test', { method: 'POST' });
            const body = await resp.json();
            if (!resp.ok) {
                return { success: false, error: body.error || ('HTTP ' + resp.status) };
            }
            return { success: true, delivered: body.delivered, total: body.total };
        } catch (err) {
            return { success: false, error: err.message || String(err) };
        }
    },
};
