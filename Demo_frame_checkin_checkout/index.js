const apiUrl = 'https://192.168.1.35:5001/api/TimeSkip/attendances'; // Thay đúng URL API của bạn

function formatDateTime(dateStr) {
    if (!dateStr) return "Chưa checkout";
    const d = new Date(dateStr);
    return d.toLocaleTimeString('vi-VN', {
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit'
    });
}

function loadAttendances() {
    const statusEl = document.getElementById('status');
    const tableEl = document.getElementById('attendanceTable');
    const tbody = document.getElementById('attendanceBody');

    fetch(apiUrl)
        .then(res => {
            if (!res.ok) throw new Error(`HTTP ${res.status}`);
            return res.json();
        })
        .then(data => {
            if (data.length === 0) {
                statusEl.textContent = "Không có dữ liệu check-in/checkout.";
                return;
            }

            tbody.innerHTML = "";
            data.forEach(item => {
                const tr = document.createElement('tr');
                tr.innerHTML = `
                    <td>${item.index}</td>
                    <td>${item.employeeFullName}</td>
                    <td>${item.employeeEmail}</td>
                    <td>${item.workDate}</td>
                    <td>${formatDateTime(item.checkIn)}</td>
                    <td>${formatDateTime(item.checkOut)}</td>
                `;
                tbody.appendChild(tr);
            });

            statusEl.style.display = 'none';
            tableEl.style.display = 'table';
        })
        .catch(error => {
            statusEl.textContent = "Lỗi khi tải dữ liệu: " + error.message;
            statusEl.classList.remove('loading');
            statusEl.classList.add('error');
        });
}

window.addEventListener('DOMContentLoaded', loadAttendances);
