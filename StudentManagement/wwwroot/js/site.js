// ─── Clock ────────────────────────────────────────────────────────────────
function updateClock() {
  const el = document.getElementById('topbarTime');
  if (!el) return;
  const now = new Date();
  el.textContent = now.toLocaleTimeString('en-ZA', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
}
updateClock();
setInterval(updateClock, 1000);

// ─── Alert Dismiss ─────────────────────────────────────────────────────────
document.querySelectorAll('.alert-close').forEach(btn => {
  btn.addEventListener('click', () => {
    btn.closest('.alert').style.animation = 'slideDown 0.25s ease reverse';
    setTimeout(() => btn.closest('.alert').remove(), 250);
  });
});

// Auto-dismiss alerts after 5s
document.querySelectorAll('.alert').forEach(alert => {
  setTimeout(() => {
    if (alert.parentNode) {
      alert.style.opacity = '0';
      alert.style.transition = 'opacity 0.5s';
      setTimeout(() => alert.remove(), 500);
    }
  }, 5000);
});

// ─── Confirm Modal ─────────────────────────────────────────────────────────
const modal   = document.getElementById('confirmModal');
const modalTitle   = document.getElementById('modalTitle');
const modalMessage = document.getElementById('modalMessage');
const modalIcon    = document.getElementById('modalIcon');
const modalConfirm = document.getElementById('modalConfirm');
const modalCancel  = document.getElementById('modalCancel');

let pendingForm = null;

function showConfirm({ title, message, type = 'danger', onConfirm }) {
  modalTitle.textContent   = title;
  modalMessage.textContent = message;

  if (type === 'soft') {
    modalIcon.className = 'modal-icon soft';
    modalIcon.innerHTML = '<i class="fa-solid fa-user-slash"></i>';
    modalConfirm.className = 'btn btn-ghost';
    modalConfirm.style.borderColor = 'var(--amber)';
    modalConfirm.style.color = 'var(--amber)';
  } else {
    modalIcon.className = 'modal-icon';
    modalIcon.innerHTML = '<i class="fa-solid fa-triangle-exclamation"></i>';
    modalConfirm.className = 'btn btn-danger';
    modalConfirm.style.borderColor = '';
    modalConfirm.style.color = '';
  }

  modal.style.display = 'flex';
  pendingForm = onConfirm;
}

if (modalConfirm) {
  modalConfirm.addEventListener('click', () => {
    if (pendingForm) pendingForm();
    modal.style.display = 'none';
    pendingForm = null;
  });
}

if (modalCancel) {
  modalCancel.addEventListener('click', () => {
    modal.style.display = 'none';
    pendingForm = null;
  });
}

if (modal) {
  modal.addEventListener('click', e => {
    if (e.target === modal) {
      modal.style.display = 'none';
      pendingForm = null;
    }
  });
}

// ─── Soft Delete buttons ───────────────────────────────────────────────────
document.querySelectorAll('[data-soft-delete]').forEach(btn => {
  btn.addEventListener('click', e => {
    e.preventDefault();
    const name = btn.getAttribute('data-student-name') || 'this student';
    const form = btn.closest('form') || document.getElementById(btn.getAttribute('data-form-id'));
    showConfirm({
      title: 'Mark as Inactive?',
      message: `This will mark "${name}" as inactive. You can reactivate them later by editing.`,
      type: 'soft',
      onConfirm: () => form && form.submit()
    });
  });
});

// ─── Hard Delete buttons ──────────────────────────────────────────────────
document.querySelectorAll('[data-hard-delete]').forEach(btn => {
  btn.addEventListener('click', e => {
    e.preventDefault();
    const name = btn.getAttribute('data-student-name') || 'this student';
    const form = btn.closest('form') || document.getElementById(btn.getAttribute('data-form-id'));
    showConfirm({
      title: 'Permanently Delete?',
      message: `This will permanently remove "${name}" and all their data. This cannot be undone.`,
      type: 'danger',
      onConfirm: () => form && form.submit()
    });
  });
});

// ─── Image Upload Preview ──────────────────────────────────────────────────
const fileInput = document.getElementById('profileImageInput');
const previewImg = document.getElementById('imagePreview');
const uploadArea = document.querySelector('.image-upload-area');
const previewWrap = document.getElementById('imagePreviewWrap');
const uploadPlaceholder = document.getElementById('uploadPlaceholder');

if (fileInput) {
  fileInput.addEventListener('change', handleImageChange);

  // Drag & drop
  if (uploadArea) {
    uploadArea.addEventListener('dragover', e => {
      e.preventDefault();
      uploadArea.classList.add('dragover');
    });
    uploadArea.addEventListener('dragleave', () => uploadArea.classList.remove('dragover'));
    uploadArea.addEventListener('drop', e => {
      e.preventDefault();
      uploadArea.classList.remove('dragover');
      const file = e.dataTransfer.files[0];
      if (file) {
        fileInput.files = e.dataTransfer.files;
        previewFile(file);
      }
    });
  }
}

function handleImageChange(e) {
  const file = e.target.files[0];
  if (file) previewFile(file);
}

function previewFile(file) {
  const allowed = ['image/jpeg', 'image/png'];
  if (!allowed.includes(file.type)) {
    alert('Only JPG and PNG files are allowed.');
    return;
  }
  if (file.size > 5 * 1024 * 1024) {
    alert('File must be under 5MB.');
    return;
  }
  const reader = new FileReader();
  reader.onload = e => {
    if (previewImg) previewImg.src = e.target.result;
    if (previewWrap) previewWrap.style.display = 'inline-flex';
    if (uploadPlaceholder) uploadPlaceholder.style.display = 'none';
  };
  reader.readAsDataURL(file);
}

// ─── Search autocomplete ──────────────────────────────────────────────────
const liveSearch = document.getElementById('liveSearchInput');
if (liveSearch) {
  let searchTimeout;
  liveSearch.addEventListener('input', () => {
    clearTimeout(searchTimeout);
    searchTimeout = setTimeout(() => {
      const form = liveSearch.closest('form');
      if (form) form.submit();
    }, 500);
  });
}

// ─── Sidebar toggle ──────────────────────────────────────────────────────
const sidebarToggle = document.getElementById('sidebarToggle');
const sidebar = document.getElementById('sidebar');
const mainContent = document.querySelector('.main-content');

if (sidebarToggle && sidebar) {
  sidebarToggle.addEventListener('click', () => {
    const isCollapsed = sidebar.style.width === 'var(--sidebar-w-collapsed)' ||
                        sidebar.clientWidth < 100;
    sidebar.style.width = isCollapsed ? 'var(--sidebar-w)' : 'var(--sidebar-w-collapsed)';
    if (mainContent) {
      mainContent.style.marginLeft = isCollapsed ? 'var(--sidebar-w)' : 'var(--sidebar-w-collapsed)';
    }
    document.querySelectorAll('.brand-text, .nav-item span, .admin-info, .logout-btn span, .nav-label')
      .forEach(el => {
        el.style.display = isCollapsed ? '' : 'none';
      });
  });
}
