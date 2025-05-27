document.querySelector('.btn-export').addEventListener('click', function () {
    const table = document.querySelector('table');
    const html = table.outerHTML;

    const blob = new Blob(["\ufeff", html], {
      type: "application/vnd.ms-excel"
    });

    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'bang_tong_hop_cong.xls'; // Tên file
    a.click();
    URL.revokeObjectURL(url);
  });