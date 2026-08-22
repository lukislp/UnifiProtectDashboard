// Receives Web Push notifications (see Controllers/PushController.cs, BackgroundServices/
// DailyDigestService.cs - both go through NotifyHub's NotificationSender). Served at
// /service-worker.js by the existing static-files middleware, which is what gives it origin-wide
// scope. Payload shape is exactly what NotifyHub's WebPushChannel sends:
// {title, body, url, data, image, silent, tag}.
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()));

self.addEventListener('push', event => {
    let data = {};
    try {
        data = event.data ? event.data.json() : {};
    } catch {
        data = { title: 'UnifiProtectDashboard', body: event.data ? event.data.text() : '' };
    }

    const title = data.title || 'UnifiProtectDashboard';
    const options = {
        body: data.body || '',
        icon: data.image || '/favicon.png',
        tag: data.tag || 'unifiprotectdashboard',
        renotify: true,
        silent: !!data.silent,
        data: data.url ? { url: data.url } : {},
    };

    event.waitUntil(self.registration.showNotification(title, options));
});

self.addEventListener('notificationclick', event => {
    event.notification.close();
    const url = event.notification.data && event.notification.data.url;
    event.waitUntil(
        self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then(clients => {
            if (clients.length > 0) return clients[0].focus();
            return self.clients.openWindow(url || '/');
        })
    );
});
