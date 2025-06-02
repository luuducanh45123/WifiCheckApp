document.addEventListener("DOMContentLoaded", () => {
  const loadBtn = document.getElementById("load-data");
  const tbody = document.getElementById("days-body");
  const userId = localStorage.getItem("userId");

  const selectedDateInput = document.getElementById("date");

  loadBtn.addEventListener("click", () => {
    const selectedDate = selectedDateInput.value;

    if (!selectedDate) {
      alert("Vui lòng chọn ngày");
      return;
    }

    fetch(`https://localhost:5125/api/TimeSkip/by-date?date=${selectedDate}`)
      .then(res => {
        if (!res.ok) throw new Error("Lỗi khi tải dữ liệu");
        return res.json();
      })
      .then(data => {
        tbody.innerHTML = "";

        data.forEach(row => {
          const tr = document.createElement("tr");
          tr.dataset.attendanceId = row.attendanceId; // Cần có trong API để truyền

          tr.innerHTML = `
            <td>${row.stt}</td>
            <td>${row.employeeName}</td>
            <td><input type="time" class="check-in" value="${row.checkInMorning || ""}" /></td>
            <td><input type="time" class="check-out" value="${row.checkOutMorning || ""}" /></td>
            <td><input type="time" class="check-in" value="${row.checkInAfternoon || ""}" /></td>
            <td><input type="time" class="check-out" value="${row.checkOutAfternoon || ""}" /></td>
            <td><input type="text" class="reason" placeholder="Nhập lý do" /></td>
            <td><button type="button" class="save-btn">Lưu</button></td>
          `;

          tbody.appendChild(tr);
        });

        attachSaveHandlers();
      })
      .catch(err => {
        console.error(err);
        alert("Không thể tải dữ liệu. Vui lòng thử lại.");
      });
  });

  function attachSaveHandlers() {
    document.querySelectorAll(".save-btn").forEach(button => {
      button.addEventListener("click", async function () {
        const row = this.closest("tr");
        const attendanceId = row.dataset.attendanceId;
        const checkIns = row.querySelectorAll(".check-in");
        const checkOuts = row.querySelectorAll(".check-out");
        const reason = row.querySelector(".reason").value;

        const selectedDate = selectedDateInput.value;
        const performedBy = userId;

        const payload = {
          attendanceId: Number(attendanceId),
          checkInTime: checkIns[0].value ? `${selectedDate}T${checkIns[0].value}:00` : null,
          checkOutTime: checkOuts[0].value ? `${selectedDate}T${checkOuts[0].value}:00` : null,
          reason: reason,
          performedBy: performedBy
        };

        try {
          const res = await fetch("https://localhost:5125/api/attendance/adjust", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
          });

          const data = await res.json();
          alert(data.message || "Đã lưu!");
        } catch (error) {
          console.error(error);
          alert("Không thể lưu dữ liệu.");
        }
      });
    });
  }
});
