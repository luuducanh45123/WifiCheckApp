// File: /js/admin.js

document.addEventListener('DOMContentLoaded', function () {
  // === 1. Lấy các phần tử (selector) theo đúng HTML hiện tại ===
  const userName = document.getElementById('user-name');
  const dropdownMenu = document.getElementById('dropdown-menu');
  const logoutLink = document.getElementById('logout');

  const tableBody = document.getElementById('table-body');
  const monthInput = document.getElementById('month');
  const leaveTypeSelect = document.getElementById('type');
  const nameInput = document.getElementById('search-name');
  const exportButton = document.getElementById('export-btn');

  const popup = document.getElementById('popup');
  const popupEmployeeId = document.getElementById('popup-employeeId');
  const popupPaid = document.getElementById('popup-paid');
  const confirmBtn = document.getElementById('confirm-btn');
  const cancelBtn = document.getElementById('cancel-btn');

  // Biến toàn cục tạm lưu khi click vào nút "Duyệt đơn" (Approve)
  let currentEmpId = '';
  let currentPaid = 0;
  let currentUnpaid = 0;
  let currentPending = 0;

  // === 2. Thiết lập giá trị mặc định cho input tháng ===
  // Nếu bạn muốn khởi động input tháng bằng tháng hiện tại
  const now = new Date();
  // Lấy chuỗi YYYY-MM
  monthInput.value = now.toISOString().slice(0, 7);

  // === 3. Xử lý dropdown user (Admin / Logout) ===
  userName.addEventListener('click', function (e) {
    // Khi click vào "Admin", bật/tắt dropdown-menu
    // Nếu đang block thì hide, ngược lại show
    dropdownMenu.style.display = dropdownMenu.style.display === 'block' ? 'none' : 'block';
    // Dừng nổi bọt event, tránh click ra ngoài liền tắt ngay
    e.stopPropagation();
  });

  // Khi click ra ngoài vùng #user-dropdown, tự động ẩn dropdown-menu
  document.addEventListener('click', function (e) {
    const userDropdownContainer = document.getElementById('user-dropdown');
    if (userDropdownContainer && !userDropdownContainer.contains(e.target)) {
      dropdownMenu.style.display = 'none';
    }
  });

  // Xử lý logout: xóa token, chuyển về login
  logoutLink.addEventListener('click', function (e) {
    e.preventDefault();
    localStorage.removeItem('token');
    localStorage.removeItem('userName');
    window.location.href = '/html/login.html';
  });

  // Load lại tên user từ localStorage (nếu có) để hiển thị
  const storedName = localStorage.getItem('userName');
  if (storedName) {
    userName.textContent = storedName;
  }

  // === 4. Đổ dữ liệu "Loại công" (leave-types) vào select ===
  fetch('https://localhost:5125/api/TimeSkip/leave-types')
    .then(response => {
      if (!response.ok) throw new Error('Lỗi khi tải loại công');
      return response.json();
    })
    .then(data => {
      // Tạo option mặc định
      const defaultOption = document.createElement('option');
      defaultOption.value = '';
      defaultOption.textContent = '-- Chọn loại công --';
      leaveTypeSelect.appendChild(defaultOption);

      data.forEach(item => {
        const option = document.createElement('option');
        option.value = item.value;   // ví dụ "Paid" hoặc "Unpaid"
        option.textContent = item.text; // ví dụ "Nghỉ phép", "Nghỉ không phép"
        leaveTypeSelect.appendChild(option);
      });
    })
    .catch(error => {
      console.error('Lỗi tải loại công:', error);
      alert('Không thể tải loại công. Vui lòng thử lại.');
    });

  // === 5. Hàm loadData(): gọi API và render vào bảng ===
  function loadData() {
    const selectedMonth = monthInput.value; // "YYYY-MM"
    const nameFilter = nameInput.value.trim().toLowerCase();
    const selectedLeaveType = leaveTypeSelect.value; // "" hoặc "Paid"/"Unpaid"

    // Kiểm tra tháng bắt buộc là "YYYY-MM"
    if (!selectedMonth || selectedMonth.length !== 7) {
      tableBody.innerHTML = '<tr><td colspan="9">Vui lòng chọn tháng hợp lệ.</td></tr>';
      return;
    }

    // Gọi API summary
    fetch(`https://localhost:5125/api/TimeSkip/summary?month=${selectedMonth}`)
      .then(response => {
        if (!response.ok) throw new Error('Lỗi khi gọi API summary');
        return response.json();
      })
      .then(data => {
        // Xóa hết nội dung cũ
        tableBody.innerHTML = '';

        // Lọc theo tên và loại công (nếu có chọn)
        const filtered = data.filter(emp => {
          const matchName = nameFilter
            ? emp.fullName.toLowerCase().includes(nameFilter)
            : true;

          let matchLeaveType = true;
          if (selectedLeaveType) {
            if (selectedLeaveType === 'Paid') {
              matchLeaveType = emp.totalPaidLeaves > 0;
            } else if (selectedLeaveType === 'Unpaid') {
              matchLeaveType = emp.totalUnpaidLeaves > 0;
            }
          }

          return matchName && matchLeaveType;
        });

        // Nếu không có dữ liệu sau lọc
        if (filtered.length === 0) {
          tableBody.innerHTML = '<tr><td colspan="9">Không tìm thấy nhân viên nào.</td></tr>';
          return;
        }

        // Duyệt mảng filtered và render từng row
        filtered.forEach(emp => {
          const row = document.createElement('tr');
          row.innerHTML = `
            <td>${emp.employeeId || '-'}</td>
            <td>${emp.fullName}</td>
            <td>${emp.totalWorkingDays}</td>
            <td>${emp.totalPaidLeaves}</td>
            <td>${emp.totalUnpaidLeaves}</td>
            <td>${emp.totalLateSessions}</td>
            <td>${emp.totalPendingLeaves || 0}</td>
            <td>${emp.totalLateMinutes}</td>
            <td>
              <button class="approve-btn" 
                      data-id="${emp.employeeId}" 
                      data-paid="${emp.totalPaidLeaves}" 
                      data-unpaid="${emp.totalUnpaidLeaves}" 
                      data-pending="${emp.totalPendingLeaves || 0}">
                Duyệt đơn
              </button>
              <button class="reject-btn" data-id="${emp.employeeId}">
                Từ chối
              </button>
            </td>
          `;
          tableBody.appendChild(row);
        });

        // Sau khi render xong, attach event cho từng nút
        attachPopupEvents();
      })
      .catch(error => {
        console.error('Lỗi load data:', error);
        tableBody.innerHTML = '<tr><td colspan="9">Không thể tải dữ liệu.</td></tr>';
      });
  }

  // Gọi loadData mỗi khi thay đổi tháng / tên / loại nghỉ
  monthInput.addEventListener('change', loadData);
  nameInput.addEventListener('input', loadData);
  leaveTypeSelect.addEventListener('change', loadData);

  // Nếu trang mới load, gọi 1 lần để hiện bảng mặc định theo tháng hiện tại
  if (monthInput.value) {
    loadData();
  }

  // === 6. Hàm open/close Popup và đẩy dữ liệu vào ===
  function openPopup(employeeId, paidLeave, unpaidLeave, pendingLeave) {
    currentEmpId = employeeId;
    currentPaid = Number(paidLeave);
    currentUnpaid = Number(unpaidLeave);
    currentPending = Number(pendingLeave);

    // Chỉ có popupEmployeeId và popupPaid trong HTML mới
    if (popupEmployeeId) {
      popupEmployeeId.textContent = employeeId;
    }
    if (popupPaid) {
      popupPaid.textContent = pendingLeave;
    }

    popup.classList.remove('hidden');
  }

  function closePopup() {
    popup.classList.add('hidden');
  }

  // === 7. Gắn sự kiện cho các nút Approve / Reject vừa tạo ở table ===
  function attachPopupEvents() {
    // 7.1. Approve-buttons: mở popup
    const approveButtons = document.querySelectorAll('.approve-btn');
    approveButtons.forEach(button => {
      button.addEventListener('click', () => {
        const empId = button.getAttribute('data-id');
        const paid = button.getAttribute('data-paid');
        const unpaid = button.getAttribute('data-unpaid');
        const pending = button.getAttribute('data-pending');
        openPopup(empId, paid, unpaid, pending);
      });
    });

    // 7.2. Reject-buttons: gửi API ngay khi confirm người dùng
    const rejectButtons = document.querySelectorAll('.reject-btn');
    rejectButtons.forEach(button => {
      button.addEventListener('click', () => {
        const empId = button.getAttribute('data-id');
        const selectedMonth = monthInput.value;

        if (!confirm(`Bạn chắc chắn muốn từ chối đơn của nhân viên ${empId}?`)) {
          return;
        }

        fetch('https://localhost:5125/api/TimeSkip/convert-unpaid', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            employeeIds: [empId],
            month: selectedMonth,
            isApproved: false
          })
        })
          .then(response => {
            if (!response.ok) throw new Error('Lỗi khi từ chối đơn');
            return response.json();
          })
          .then(result => {
            alert(result.message || 'Từ chối đơn thành công!');
            loadData();
          })
          .catch(error => {
            console.error('Lỗi khi reject:', error);
            alert(`Từ chối đơn thất bại: ${error.message}`);
          });
      });
    });
  }

  // === 8. Xử lý confirm/cancel trong popup ===
  if (confirmBtn) {
    confirmBtn.addEventListener('click', () => {
      const selectedMonth = monthInput.value;
      fetch('https://localhost:5125/api/TimeSkip/convert-unpaid', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          employeeIds: [currentEmpId],
          month: selectedMonth,
          isApproved: true
        })
      })
        .then(response => {
          if (!response.ok) throw new Error('Lỗi khi duyệt đơn');
          return response.json();
        })
        .then(result => {
          alert(result.message || 'Duyệt đơn thành công!');
          closePopup();
          loadData();
        })
        .catch(error => {
          console.error('Lỗi khi approve:', error);
          alert(`Duyệt đơn thất bại: ${error.message}`);
        });
    });
  }

  if (cancelBtn) {
    cancelBtn.addEventListener('click', closePopup);
  }

  // === 9. Export sang Excel ===
  exportButton.addEventListener('click', exportToExcel);

  function exportToExcel() {
    const selectedMonth = monthInput.value;
    const nameFilter = nameInput.value.trim().toLowerCase();
    const selectedLeaveType = leaveTypeSelect.value;

    fetch(`https://localhost:5125/api/TimeSkip/summary?month=${selectedMonth}`)
      .then(response => {
        if (!response.ok) throw new Error('Lỗi khi gọi API summary');
        return response.json();
      })
      .then(data => {
        // Lọc tương tự loadData()
        const filtered = data.filter(emp => {
          const matchName = nameFilter
            ? emp.fullName.toLowerCase().includes(nameFilter)
            : true;

          let matchLeaveType = true;
          if (selectedLeaveType) {
            if (selectedLeaveType === 'Paid') {
              matchLeaveType = emp.totalPaidLeaves > 0;
            } else if (selectedLeaveType === 'Unpaid') {
              matchLeaveType = emp.totalUnpaidLeaves > 0;
            }
          }
          return matchName && matchLeaveType;
        });

        if (filtered.length === 0) {
          alert('Không có dữ liệu để xuất.');
          return;
        }

        // Chuẩn bị dữ liệu JSON chuyển sang Excel
        const excelData = filtered.map(emp => ({
          'Mã Nhân Viên': emp.employeeId || '-',
          'Họ Và Tên': emp.fullName,
          'Tổng công làm việc trong tháng': emp.totalWorkingDays,
          'Nghỉ có phép': emp.totalPaidLeaves,
          'Nghỉ không phép': emp.totalUnpaidLeaves,
          'Tổng số buổi đi muộn': emp.totalLateSessions,
          'Đơn đang chờ duyệt': emp.totalPendingLeaves || 0,
          'Tổng thời gian đi muộn': emp.totalLateMinutes
        }));

        // Tạo workbook
        const worksheet = XLSX.utils.json_to_sheet(excelData);
        worksheet['!cols'] = [
          { wch: 15 }, // Mã Nhân Viên
          { wch: 25 }, // Họ Và Tên
          { wch: 30 }, // Tổng công
          { wch: 15 }, // Nghỉ có phép
          { wch: 20 }, // Nghỉ không phép
          { wch: 20 }, // Tổng buổi đi muộn
          { wch: 20 }, // Đơn chờ duyệt
          { wch: 25 }  // Tổng thời gian đi muộn
        ];

        const workbook = XLSX.utils.book_new();
        XLSX.utils.book_append_sheet(workbook, worksheet, 'BangTongHopCong');

        // Xuất file
        XLSX.writeFile(workbook, `BangTongHopCong_${selectedMonth}.xlsx`);
      })
      .catch(error => {
        console.error('Lỗi xuất Excel:', error);
        alert('Không thể xuất file Excel.');
      });
  }

});
