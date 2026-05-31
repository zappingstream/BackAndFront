// Service Worker básico para cumplir con el requisito de instalación de PWA
self.addEventListener('install', (event) => {
  self.skipWaiting();
});

self.addEventListener('activate', (event) => {
  return self.clients.claim();
});

self.addEventListener('fetch', (event) => {
  // No hacemos nada especial, dejamos que las peticiones a Firebase y YouTube fluyan normalmente
});