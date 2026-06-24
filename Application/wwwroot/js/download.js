// Triggers a browser file download for a same-origin URL via a transient anchor.
// Cookies are sent automatically, so cookie-based auth still applies.
window.downloadFile = function (url) {
    const a = document.createElement('a');
    a.href = url;
    a.style.display = 'none';
    document.body.appendChild(a);
    a.click();
    a.remove();
};
