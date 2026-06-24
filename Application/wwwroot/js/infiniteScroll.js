// Infinite scroll via IntersectionObserver.
// Watches a sentinel element and invokes a .NET callback when it becomes visible.

window.infiniteScroll = (function () {
    const _observers = {};

    function init(sentinelId, dotNetRef, callbackMethod) {
        var sentinel = document.getElementById(sentinelId);
        if (!sentinel) return;

        // Re-init is idempotent: drop any observer previously bound to this id
        // (the sentinel element moves as new pages are appended).
        var existing = _observers[sentinelId];
        if (existing) {
            existing.disconnect();
            delete _observers[sentinelId];
        }

        var observer = new IntersectionObserver(function (entries) {
            if (entries[0].isIntersecting) {
                dotNetRef.invokeMethodAsync(callbackMethod);
            }
        }, { threshold: 0.1 });

        observer.observe(sentinel);
        _observers[sentinelId] = observer;
    }

    function dispose(sentinelId) {
        var observer = _observers[sentinelId];
        if (observer) {
            observer.disconnect();
            delete _observers[sentinelId];
        }
    }

    return { init: init, dispose: dispose };
})();
