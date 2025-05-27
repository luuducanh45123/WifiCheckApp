fetch('/html/header_menu.html')
  .then(response => response.text())
  .then(data => {
    document.getElementById('header').innerHTML = data;

    // Lấy tên từ localStorage
    const fullName = localStorage.getItem("fullName");

    if (fullName) {
      const userNameSpan = document.getElementById("user-name");
      userNameSpan.textContent = fullName + " ▼";

      // Hiện/ẩn dropdown
      userNameSpan.addEventListener("click", () => {
        const dropdown = document.getElementById("dropdown-menu");
        dropdown.style.display = dropdown.style.display === "block" ? "none" : "block";
      });

      // Đăng xuất
      document.getElementById("logout").addEventListener("click", () => {
        localStorage.clear();
        window.location.href = "/html/login.html";
      });
    }
  })
  .catch(err => console.error('Lỗi tải header:', err));
