


function updateCartCount() {
    fetch('/Cart/GetCartCount')
    .then(response => response.json())
    .then(data => {
        const cartCountElement = document.getElementById('cart-count');
        if (cartCountElement && data.count !== undefined) {
            cartCountElement.textContent = data.count;
            if (data.count > 0) {
                cartCountElement.style.display = 'inline-block';
            } else {
                cartCountElement.style.display = 'none';
            }
        }
    })
    .catch(error => {
        console.error('Error fetching cart count:', error);
    });
}


document.addEventListener('DOMContentLoaded', function() {
    updateCartCount();
});
