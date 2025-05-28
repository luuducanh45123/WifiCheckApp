document.addEventListener("DOMContentLoaded", () => {
  const calendar = document.getElementById("calendar");
  const calendarTitle = document.getElementById("calendar-title");
  const monthSelect = document.getElementById("month-select");
  const yearSelect = document.getElementById("year-select");
  const modal = document.getElementById("updateModal");
  const closeModalBtn = document.getElementById("closeModal");
  const updateForm = document.getElementById("updateForm");

  const role = localStorage.getItem("role");
  const token = localStorage.getItem("token");
  const employeeIdRaw = localStorage.getItem("employeeId");
  const employeeId = employeeIdRaw ? parseInt(employeeIdRaw) : null;

  function populateMonthYearSelectors() {
    for (let y = 2020; y <= 2030; y++) {
      const opt = document.createElement("option");
      opt.value = y;
      opt.textContent = y;
      yearSelect.appendChild(opt);
    }

    for (let m = 0; m < 12; m++) {
      const opt = document.createElement("option");
      opt.value = m;
      opt.textContent = `Tháng ${m + 1}`;
      monthSelect.appendChild(opt);
    }

    const today = new Date();
    yearSelect.value = today.getFullYear();
    monthSelect.value = today.getMonth();
  }

  async function generateCalendar(year, month) {
    calendar.innerHTML = "";

    const apiUrl = `https://localhost:5001/api/TimeSkip/attendance/monthly?employeeId=${employeeId}&month=${month + 1}&year=${year}`;
    let attendanceData = [];

    try {
      const response = await fetch(apiUrl);
      if (response.ok) {
        attendanceData = await response.json();
      } else {
        console.error("API error:", response.status);
      }
    } catch (err) {
      console.error("Fetch failed:", err);
    }

    const daysOfWeek = ["Thứ 2", "Thứ 3", "Thứ 4", "Thứ 5", "Thứ 6", "Thứ 7", "Chủ nhật"];
    const firstDay = new Date(year, month, 1).getDay();
    const daysInMonth = new Date(year, month + 1, 0).getDate();
    const today = new Date();

    let startDay = firstDay === 0 ? 6 : firstDay - 1;
    const grid = document.createElement("div");
    grid.className = "calendar-grid-inner";

    // Header thứ
    daysOfWeek.forEach(day => {
      const cell = document.createElement("div");
      cell.className = "calendar-cell header";
      cell.textContent = day;
      grid.appendChild(cell);
    });

    // Ô trống đầu tháng
    for (let i = 0; i < startDay; i++) {
      const emptyCell = document.createElement("div");
      emptyCell.className = "calendar-cell empty";
      grid.appendChild(emptyCell);
    }

    // Các ngày trong tháng
    for (let date = 1; date <= daysInMonth; date++) {
      const cell = document.createElement("div");
      cell.className = "calendar-cell day";

      const currentDate = new Date(year, month, date);
      const dayOfWeek = currentDate.getDay(); // 0 = Sunday

      if (dayOfWeek === 0) {
        cell.classList.add("sunday");
      }

      const attendance = attendanceData.find(a => a.day === date);
      let contentHtml = `<strong>${date}</strong><br>`;

      if (currentDate <= today) {
        const isSunday = dayOfWeek === 0;

        if (isSunday) {
          contentHtml += `<span class="no-data">Chủ nhật</span>`;
        } else {
          contentHtml += `
          <span class="checkin">Sáng: ${attendance?.morningCheckIn || "Vắng"} - ${attendance?.morningCheckOut || "Vắng"}</span><br>
          <span class="checkout">Chiều: ${attendance?.afternoonCheckIn || "Vắng"} - ${attendance?.afternoonCheckOut || "Vắng"}</span>
          `;
        }
      }

      cell.innerHTML = contentHtml;

      // Thêm sự kiện click mở modal chỉ với những ngày hợp lệ (không phải Chủ nhật)
      if (currentDate <= today && dayOfWeek !== 0) {
        cell.style.cursor = "pointer";
        cell.addEventListener("click", () => {
          // Hiển thị modal
          modal.style.display = "flex";

          // Điền dữ liệu cũ vào form
          document.getElementById("morningCheckIn").value = attendance?.morningCheckIn || "";
          document.getElementById("morningCheckOut").value = attendance?.morningCheckOut || "";
          document.getElementById("afternoonCheckIn").value = attendance?.afternoonCheckIn || "";
          document.getElementById("afternoonCheckOut").value = attendance?.afternoonCheckOut || "";

          // Lưu lại thông tin ngày hiện tại để dùng khi gửi API
          modal.dataset.year = year;
          modal.dataset.month = month;
          modal.dataset.date = date;
        });
      }

      grid.appendChild(cell);
    }

    calendar.appendChild(grid);
    calendarTitle.textContent = `BẢNG CHẤM CÔNG THÁNG ${month + 1}/${year}`;
  }

  // Đóng modal khi bấm nút đóng
  closeModalBtn.onclick = () => {
    modal.style.display = "none";
  };

  // Đóng modal khi click ngoài nội dung modal
  window.onclick = function(event) {
    if (event.target == modal) {
      modal.style.display = "none";
    }
  };

  // Xử lý submit form cập nhật chấm công
  updateForm.onsubmit = async (e) => {
    e.preventDefault();

    if (role?.toLowerCase() !== "admin") {
      alert("Bạn không có quyền cập nhật dữ liệu.");
      return;  // Dừng không gửi request
    }


    const morningIn = document.getElementById("morningCheckIn").value;
    const morningOut = document.getElementById("morningCheckOut").value;
    const afternoonIn = document.getElementById("afternoonCheckIn").value;
    const afternoonOut = document.getElementById("afternoonCheckOut").value;

    const year = parseInt(modal.dataset.year);
    const month = parseInt(modal.dataset.month);
    const date = parseInt(modal.dataset.date);

    const isoDate = new Date(year, month, date).toISOString().split('T')[0]; // yyyy-mm-dd

    try {
      const res = await fetch(`https://localhost:5001/api/Admin/${employeeId}`, {
        method: "PUT",
        headers: {
          "Content-Type": "application/json",
          "Authorization": `Bearer ${token}`
        },
        body: JSON.stringify({
          date: isoDate,
          morningCheckIn: morningIn,
          morningCheckOut: morningOut,
          afternoonCheckIn: afternoonIn,
          afternoonCheckOut: afternoonOut
        })
      });

      if (res.ok) {
        alert("Cập nhật thành công!");
        modal.style.display = "none";
        generateCalendar(year, month); // reload lại lịch để cập nhật dữ liệu mới
      } else {
        alert("Lỗi cập nhật");
      }
    } catch (err) {
      alert("Lỗi kết nối API");
      console.error(err);
    }
  };

  // Sự kiện khi chọn tháng/năm
  monthSelect.addEventListener("change", () => {
    generateCalendar(+yearSelect.value, +monthSelect.value);
  });

  yearSelect.addEventListener("change", () => {
    generateCalendar(+yearSelect.value, +monthSelect.value);
  });

  // Gọi khi khởi tạo
  populateMonthYearSelectors();
  generateCalendar(+yearSelect.value, +monthSelect.value);
});
