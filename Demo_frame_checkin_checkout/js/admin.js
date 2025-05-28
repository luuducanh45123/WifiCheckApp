document.addEventListener('DOMContentLoaded', function () {
  const userName = document.getElementById('user-name');
  const dropdownMenu = document.getElementById('dropdown-menu');
  const logoutLink = document.getElementById('logout');
  const tableBody = document.getElementById('table-body');
  const monthInput = document.getElementById('month');
  const leaveTypeSelect = document.getElementById('type');
  const nameInput = document.getElementById('search-name');

  const popup = document.getElementById('popup');
  const popupEmployeeId = document.getElementById('popup-employeeId');
  const popupEmployeeName = document.getElementById('popup-employeeName');
  const popupPaid = document.getElementById('popup-paid');
  const popupPending = document.getElementById('popup-pending');
  const confirmBtn = document.getElementById('confirm-btn');
  const cancelBtn = document.getElementById('cancel-btn');

  let currentEmpId = '';
  let currentEmpName = '';
  let currentPaid = 0;
  let currentUnpaid = 0;
  let currentPending = 0;

  const now = new Date();
  monthInput.value = now.toISOString().slice(0, 7);

  userName.addEventListener('click', function () {
    dropdownMenu.style.display = dropdownMenu.style.display === 'block' ? 'none' : 'block';
  });

  document.addEventListener('click', function (e) {
    if (!document.getElementById('user-dropdown')?.contains(e.target)) {
      dropdownMenu.style.display = 'none';
    }
  });

  logoutLink.addEventListener('click', function (e) {
    e.preventDefault();
    localStorage.removeItem('token');
    localStorage.removeItem('userName');
    window.location.href = '/html/login.html';
  });

  const storedName = localStorage.getItem('userName');
  if (storedName) {
    userName.textContent = storedName;
  }

  fetch("https://localhost:5001/api/TimeSkip/leave-types")
    .then(response => response.json())
    .then(data => {
      const defaultOption = document.createElement('option');
      defaultOption.value = '';
      defaultOption.textContent = '-- Chọn loại công --';
      leaveTypeSelect.appendChild(defaultOption);

      data.forEach(item => {
        const option = document.createElement('option');
        option.value = item.value;
        option.textContent = item.text;
        leaveTypeSelect.appendChild(option);
      });
    })
    .catch(error => {
      console.error("Lỗi tải loại công:", error);
      alert("Không thể tải loại công.");
    });

  function loadData() {
    const selectedMonth = monthInput.value;
    const nameFilter = nameInput.value.trim().toLowerCase();
    const selectedLeaveType = leaveTypeSelect.value;

    if (!selectedMonth || selectedMonth.length !== 7) {
      tableBody.innerHTML = '<tr><td colspan="9">Vui lòng chọn tháng hợp lệ.</td></tr>';
      return;
    }

    fetch(`https://localhost:5001/api/TimeSkip/summary?month=${selectedMonth}`)
      .then(response => {
        if (!response.ok) throw new Error("Lỗi khi gọi API");
        return response.json();
      })
      .then(data => {
        tableBody.innerHTML = '';

        const filtered = data.filter(emp => {
          const matchName = nameFilter ? emp.fullName.toLowerCase().includes(nameFilter) : true;
          const matchLeaveType = selectedLeaveType
            ? emp.totalPaidLeaves > 0 && selectedLeaveType === 'Paid'
              || emp.totalUnpaidLeaves > 0 && selectedLeaveType === 'Unpaid'
            : true;
          return matchName && matchLeaveType;
        });

        if (filtered.length === 0) {
          tableBody.innerHTML = '<tr><td colspan="9">Không tìm thấy nhân viên nào.</td></tr>';
          return;
        }

        filtered.forEach((emp) => {
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
                      data-name="${emp.fullName}"
                      data-paid="${emp.totalPaidLeaves}" 
                      data-unpaid="${emp.totalUnpaidLeaves}" 
                      data-pending="${emp.totalPendingLeaves || 0}">
                Duyệt đơn
              </button>
              <button class="reject-btn" data-id="${emp.employeeId}">Từ chối</button>
            </td>
          `;
          tableBody.appendChild(row);
        });

        attachPopupEvents();
      })
      .catch(error => {
        console.error("Lỗi:", error);
        tableBody.innerHTML = '<tr><td colspan="9">Không thể tải dữ liệu.</td></tr>';
      });
  }

  monthInput.addEventListener('change', loadData);
  nameInput.addEventListener('input', loadData);
  leaveTypeSelect.addEventListener('change', loadData);

  if (monthInput.value) {
    loadData();
  }

  function openPopup(employeeId, employeeName, paidLeave, unpaidLeave, pendingLeave = 0) {
    currentEmpId = employeeId;
    currentEmpName = employeeName;
    currentPaid = Number(paidLeave);
    currentUnpaid = Number(unpaidLeave);
    currentPending = Number(pendingLeave);

    popupEmployeeId.textContent = employeeId;
    if (popupEmployeeName) popupEmployeeName.textContent = employeeName;
    if (popupPending) popupPending.textContent = pendingLeave;

    popup.classList.remove('hidden');
  }

  function closePopup() {
    popup.classList.add('hidden');
  }

  function attachPopupEvents() {
    const approveButtons = document.querySelectorAll('.approve-btn');
    approveButtons.forEach(button => {
      button.addEventListener('click', () => {
        const empId = button.getAttribute('data-id');
        const empName = button.getAttribute('data-name');
        const paid = button.getAttribute('data-paid');
        const unpaid = button.getAttribute('data-unpaid');
        const pending = button.getAttribute('data-pending') || 0;
        openPopup(empId, empName, paid, unpaid, pending);
      });
    });

    const rejectButtons = document.querySelectorAll('.reject-btn');
    rejectButtons.forEach(button => {
      button.addEventListener('click', () => {
        const empId = button.getAttribute('data-id');
        const selectedMonth = monthInput.value;

        if (!confirm(`Bạn chắc chắn muốn từ chối đơn của nhân viên ${empId}?`)) return;

        fetch(`https://localhost:5001/api/TimeSkip/convert-unpaid`, {
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
            console.error(error);
            alert(`Từ chối đơn thất bại: ${error.message}`);
          });
      });
    });
  }

  confirmBtn.addEventListener('click', () => {
    const selectedMonth = monthInput.value;

    fetch(`https://localhost:5001/api/TimeSkip/convert-unpaid`, {
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
        console.error(error);
        alert(`Duyệt đơn thất bại: ${error.message}`);
      });
  });

  cancelBtn.addEventListener('click', closePopup);
});