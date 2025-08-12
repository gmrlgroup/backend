





// print the object in parameter
function printObject(obj) {
    console.log(obj);
}






//const themeToggle = document.getElementById('theme-toggle');

// Check for saved theme preference or default to light
const savedTheme = localStorage.getItem('theme') || 'light';
document.documentElement.setAttribute('data-theme', savedTheme);
//themeToggle.checked = savedTheme === 'dark';

// File download function for Blazor components
function downloadFileFromByteArray(filename, contentType, byteArray) {
    const blob = new Blob([byteArray], { type: contentType });
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
}

