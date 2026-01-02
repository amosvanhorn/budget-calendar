let currentYear = new Date().getFullYear();
let currentMonth = new Date().getMonth() + 1;
let currentAccountId = 1;
let expenses = [];
let layers = [];
let accounts = [];
let defaultLayerActive = true;
let dailyBalances = {};
let currentExpense = null;
let currentAccount = null;
let recurringEditMode = null;
let balanceOverrides = {};
let currentBalanceDate = null;
const STORAGE_KEY = 'budget_calendar_data';

document.addEventListener('DOMContentLoaded', function() {
    // Get accountId from the dropdown which was set by ViewBag
    const accountSelector = document.getElementById('accountSelector');
    if (accountSelector) {
        currentAccountId = parseInt(accountSelector.value);
    }

    loadStorageToServer().then(() => {
        // Initialize sidebar state
        const stored = localStorage.getItem(STORAGE_KEY);
        if (stored) {
            const data = JSON.parse(stored);
            if (data.sidebarCollapsed) {
                document.getElementById('layersSidebar').classList.add('collapsed');
                document.getElementById('expandSidebarBtn').style.display = 'flex';
            }
        }
        loadExpenses();
    });

    document.getElementById('prevMonth').addEventListener('click', () => navigateMonth(-1));
    document.getElementById('nextMonth').addEventListener('click', () => navigateMonth(1));
    document.getElementById('clearDataBtn').addEventListener('click', clearAllData);
    document.querySelector('.close').addEventListener('click', closeModal);
    document.getElementById('expenseForm').addEventListener('submit', saveExpense);
    document.getElementById('deleteExpenseBtn').addEventListener('click', deleteExpense);
    document.getElementById('isRecurring').addEventListener('change', toggleRecurringOptions);
    document.getElementById('closeBalanceModal').addEventListener('click', closeBalanceModal);
    document.getElementById('closeRecurringEditModal').addEventListener('click', () => {
        document.getElementById('recurringEditModal').style.display = 'none';
        currentExpense = null;
    });
    document.getElementById('closeRecurringDeleteModal').addEventListener('click', () => {
        document.getElementById('recurringDeleteModal').style.display = 'none';
        document.getElementById('expenseModal').style.display = 'block';
    });
    document.getElementById('cancelBalanceBtn').addEventListener('click', closeBalanceModal);
    document.getElementById('cancelExpenseBtn').addEventListener('click', closeModal);
    document.getElementById('balanceForm').addEventListener('submit', saveBalanceOverride);
    document.getElementById('addLayerBtn').addEventListener('click', () => openLayerModal());
    document.getElementById('toggleSidebarBtn').addEventListener('click', toggleSidebar);
    document.getElementById('expandSidebarBtn').addEventListener('click', toggleSidebar);
    document.getElementById('layerForm').addEventListener('submit', createLayer);
    document.getElementById('closeLayerModal').addEventListener('click', closeLayerModal);
    document.getElementById('cancelLayerBtn').addEventListener('click', closeLayerModal);

    // Account Management events
    if (accountSelector) {
        accountSelector.addEventListener('change', (e) => {
            currentAccountId = parseInt(e.target.value);
            loadExpenses();
        });
    }

    document.getElementById('manageAccountsBtn').addEventListener('click', openAccountModal);
    document.getElementById('closeAccountModal').addEventListener('click', closeAccountModal);
    document.getElementById('accountForm').addEventListener('submit', saveAccount);
    document.getElementById('resetAccountForm').addEventListener('click', resetAccountForm);

    // Color picker logic
    document.querySelectorAll('.color-square').forEach(square => {
        const rawColor = square.dataset.color;
        if (rawColor) {
            square.style.backgroundColor = getSoftColor(rawColor);
            square.style.borderLeftColor = rawColor;
        }
        
        square.addEventListener('click', function() {
            if (this.classList.contains('add-color')) return;
            
            document.querySelectorAll('.color-square').forEach(c => c.classList.remove('selected'));
            this.classList.add('selected');
            document.getElementById('expenseColor').value = this.dataset.color;
        });
    });
});

function loadStorageToServer() {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored) {
        try {
            const data = JSON.parse(stored);
            balanceOverrides = data.balanceOverrides || {};
            defaultLayerActive = data.defaultLayerActive !== undefined ? data.defaultLayerActive : true;
            return fetch('/Expense/LoadFromStorage', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    accounts: data.accounts || [],
                    items: data.expenses || [],
                    layers: data.layers || [],
                    balanceOverrides: data.balanceOverrides || {}
                })
            }).then(response => response.json());
        } catch (e) {
            console.error('Error loading from storage:', e);
            return Promise.resolve();
        }
    }
    return Promise.resolve();
}

function saveToLocalStorage() {
    // Get all data from server and save to localStorage
    fetch('/Expense/GetAllData')
        .then(response => response.json())
        .then(data => {
            balanceOverrides = data.balanceOverrides || {};
            const storageData = {
                ...data,
                defaultLayerActive: defaultLayerActive
            };
            localStorage.setItem(STORAGE_KEY, JSON.stringify(storageData));
        })
        .catch(err => console.error('Error saving to localStorage:', err));
}

function navigateMonth(direction) {
    currentMonth += direction;
    if (currentMonth > 12) {
        currentMonth = 1;
        currentYear++;
    } else if (currentMonth < 1) {
        currentMonth = 12;
        currentYear--;
    }
    loadExpenses();
}

function toggleSidebar() {
    const sidebar = document.getElementById('layersSidebar');
    const expandBtn = document.getElementById('expandSidebarBtn');
    const isCollapsed = sidebar.classList.toggle('collapsed');
    
    expandBtn.style.display = isCollapsed ? 'flex' : 'none';
    
    // Save state to localStorage
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored) {
        const data = JSON.parse(stored);
        data.sidebarCollapsed = isCollapsed;
        localStorage.setItem(STORAGE_KEY, JSON.stringify(data));
    }
}

function openAccountModal() {
    document.getElementById('accountModal').style.display = 'block';
    loadAccounts();
}

function closeAccountModal() {
    document.getElementById('accountModal').style.display = 'none';
    resetAccountForm();
}

function loadAccounts() {
    fetch('/Expense/GetAccounts')
        .then(response => response.json())
        .then(data => {
            accounts = data;
            renderAccounts();
            updateAccountSelector();
        });
}

function renderAccounts() {
    const tbody = document.getElementById('accountTableBody');
    tbody.innerHTML = '';
    
    accounts.forEach(account => {
        const tr = document.createElement('tr');
        if (account.id === currentAccountId) tr.classList.add('current-account-row');
        
        tr.innerHTML = `
            <td>${account.name}</td>
            <td>${account.startDate.split('T')[0]}</td>
            <td>$${account.startingBalance.toFixed(2)}</td>
            <td>
                <button class="btn-icon-small" onclick="editAccount(${account.id})" title="Edit">✏️</button>
                ${accounts.length > 1 ? `<button class="btn-icon-small danger" onclick="deleteAccount(${account.id})" title="Delete">🗑️</button>` : ''}
            </td>
        `;
        tbody.appendChild(tr);
    });
}

function updateAccountSelector() {
    const selector = document.getElementById('accountSelector');
    if (!selector) return;
    
    const prevValue = selector.value;
    selector.innerHTML = '';
    
    accounts.forEach(account => {
        const option = document.createElement('option');
        option.value = account.id;
        option.textContent = account.name;
        if (account.id === currentAccountId) option.selected = true;
        selector.appendChild(option);
    });
}

function editAccount(id) {
    const account = accounts.find(a => a.id === id);
    if (!account) return;
    
    document.getElementById('editAccountId').value = account.id;
    document.getElementById('accountName').value = account.name;
    document.getElementById('accountStartBalance').value = account.startingBalance;
    document.getElementById('accountStartDate').value = account.startDate.split('T')[0];
    document.getElementById('accountDescription').value = account.description || '';
    
    document.getElementById('accountFormTitle').textContent = 'Edit Account';
    document.getElementById('saveAccountBtn').textContent = 'UPDATE ACCOUNT';
    document.getElementById('resetAccountForm').style.display = 'inline-block';
}

function resetAccountForm() {
    document.getElementById('editAccountId').value = '';
    document.getElementById('accountForm').reset();
    document.getElementById('accountFormTitle').textContent = 'Add New Account';
    document.getElementById('saveAccountBtn').textContent = 'CREATE ACCOUNT';
    document.getElementById('resetAccountForm').style.display = 'none';
}

function saveAccount(e) {
    e.preventDefault();
    
    const id = document.getElementById('editAccountId').value;
    const account = {
        id: id ? parseInt(id) : 0,
        name: document.getElementById('accountName').value,
        startingBalance: parseFloat(document.getElementById('accountStartBalance').value),
        startDate: document.getElementById('accountStartDate').value,
        description: document.getElementById('accountDescription').value
    };
    
    const url = id ? '/Expense/UpdateAccount' : '/Expense/CreateAccount';
    const method = id ? 'PUT' : 'POST';
    
    fetch(url, {
        method: method,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(account)
    })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            resetAccountForm();
            loadAccounts();
            saveToLocalStorage();
        }
    });
}

function deleteAccount(id) {
    if (id === currentAccountId) {
        alert('Cannot delete the currently active account. Switch to another account first.');
        return;
    }
    
    if (!confirm('Are you sure you want to delete this account? All associated items and layers will be permanently removed.')) return;
    
    fetch(`/Expense/DeleteAccount?id=${id}`, { method: 'DELETE' })
    .then(response => response.json())
    .then(data => {
        if (data.success) {
            loadAccounts();
            saveToLocalStorage();
        }
    });
}

function loadExpenses() {
    Promise.all([
        fetch(`/Expense/GetExpenses?year=${currentYear}&month=${currentMonth}&accountId=${currentAccountId}&defaultActive=${defaultLayerActive}`).then(r => r.json()),
        fetch(`/Expense/GetDailyBalances?year=${currentYear}&month=${currentMonth}&accountId=${currentAccountId}&defaultActive=${defaultLayerActive}`).then(r => r.json()),
        fetch(`/Expense/GetLayers`).then(r => r.json())
    ])
    .then(([expensesData, balancesData, layersData]) => {
        expenses = expensesData;
        dailyBalances = balancesData;
        // Filter layers for current account
        layers = layersData.filter(l => l.accountId === currentAccountId);
        updateMonthDisplay();
        renderCalendar();
        renderLayers();
        populateLayerDropdown();
    });
}

function updateMonthDisplay() {
    const monthNames = ['January', 'February', 'March', 'April', 'May', 'June',
                       'July', 'August', 'September', 'October', 'November', 'December'];
    document.getElementById('monthYear').textContent = `${monthNames[currentMonth - 1]} ${currentYear}`;
}

function renderCalendar() {
    const grid = document.getElementById('calendarGrid');
    grid.innerHTML = '';

    const firstDay = new Date(currentYear, currentMonth - 1, 1).getDay();
    const daysInMonth = new Date(currentYear, currentMonth, 0).getDate();

    // Empty cells before first day
    for (let i = 0; i < firstDay; i++) {
        grid.appendChild(createEmptyCell());
    }

    // Days of month
    for (let day = 1; day <= daysInMonth; day++) {
        const cell = createDayCell(day);
        grid.appendChild(cell);
    }
}

function createEmptyCell() {
    const cell = document.createElement('div');
    cell.className = 'calendar-cell empty';
    return cell;
}

function createDayCell(day) {
    const cell = document.createElement('div');
    cell.className = 'calendar-cell';

    const today = new Date();
    if (day === today.getDate() && currentMonth === (today.getMonth() + 1) && currentYear === today.getFullYear()) {
        cell.classList.add('today');
    }

    const dayNumber = document.createElement('div');
    dayNumber.className = 'day-number';
    dayNumber.textContent = day;
    cell.appendChild(dayNumber);

    const itemsWrapper = document.createElement('div');
    itemsWrapper.className = 'items-wrapper';

    const dateStr = `${currentYear}-${String(currentMonth).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
    const dayExpenses = expenses.filter(e => e.date.startsWith(dateStr));

    dayExpenses.forEach(expense => {
        const expenseItem = document.createElement('div');
        expenseItem.className = 'expense-item';
        const rawColor = expense.color || '#efebe9';
        const softBg = getSoftColor(rawColor);
        expenseItem.style.backgroundColor = softBg;
        expenseItem.style.color = getContrastColor(rawColor);
        expenseItem.style.borderLeftColor = rawColor;
        expenseItem.innerHTML = `
            <span class="expense-description">${expense.description}</span>
            <span class="expense-amount">$${expense.amount.toFixed(2)}</span>
        `;
        expenseItem.addEventListener('click', (e) => {
            e.stopPropagation();
            openModal(expense);
        });
        itemsWrapper.appendChild(expenseItem);
    });

    cell.appendChild(itemsWrapper);

    // Add balance display
    const balanceData = dailyBalances[dateStr];

    if (balanceData !== undefined) {
        const balance = balanceData.balance;
        const isOverride = balanceData.isOverride;
        
        const balanceDiv = document.createElement('div');
        balanceDiv.className = 'day-balance';
        if (balance < 0) {
            balanceDiv.classList.add('negative');
        }
        if (isOverride) {
            balanceDiv.classList.add('override');
        }
        balanceDiv.textContent = `$${balance.toFixed(2)}`;
        balanceDiv.addEventListener('click', (e) => {
            e.stopPropagation();
            openBalanceModal(dateStr, balance);
        });
        cell.appendChild(balanceDiv);
    }

    cell.addEventListener('click', () => {
        openModal(null, new Date(currentYear, currentMonth - 1, day));
    });

    return cell;
}

function openBalanceModal(dateStr, currentBalance) {
    currentBalanceDate = dateStr;
    const dateToDisplay = new Date(dateStr + 'T00:00:00');
    const options = { month: 'short', day: 'numeric', year: 'numeric' };
    document.getElementById('balanceDateSubtitle').textContent = dateToDisplay.toLocaleDateString('en-US', options);
    document.getElementById('balanceAmount').value = currentBalance.toFixed(2);
    document.getElementById('balanceModal').style.display = 'block';
}

function closeBalanceModal() {
    document.getElementById('balanceModal').style.display = 'none';
    currentBalanceDate = null;
}

function saveBalanceOverride(e) {
    e.preventDefault();
    
    const balance = parseFloat(document.getElementById('balanceAmount').value);
    
    fetch('/Expense/SetBalanceOverride', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            accountId: currentAccountId,
            date: currentBalanceDate,
            balance: balance
        })
    })
    .then(response => response.json())
    .then(() => {
        closeBalanceModal();
        saveToLocalStorage();
        loadExpenses();
    });
}

function openModal(expense = null, defaultDate = null) {
    // If editing a recurring item (parent or instance), show the edit mode dialog first
    if (expense && expense.isRecurring) {
        currentExpense = expense;
        showRecurringEditDialog();
        return;
    }

    openExpenseForm(expense, defaultDate);
}

function openExpenseForm(expense = null, defaultDate = null) {
    const modal = document.getElementById('expenseModal');
    const form = document.getElementById('expenseForm');
    const deleteBtn = document.getElementById('deleteExpenseBtn');
    const indicator = document.getElementById('editModeIndicator');
    const dateInput = document.getElementById('expenseDate');

    form.reset();
    dateInput.disabled = false;

    // Set header date subtitle
    const dateToDisplay = expense ? new Date(expense.date) : (defaultDate || new Date());
    const options = { month: 'short', day: 'numeric', year: 'numeric' };
    document.getElementById('modalSubtitle').textContent = `Scheduled for ${dateToDisplay.toLocaleDateString('en-US', options)}`;

    // Set edit mode indicator
    if (expense && expense.isRecurring) {
        indicator.style.display = 'block';
        if (recurringEditMode === 'ThisOne') {
            indicator.textContent = 'Single Instance';
            indicator.style.background = '#fef2f2';
            indicator.style.color = '#ef4444';
            indicator.style.borderColor = '#fee2e2';
        } else if (recurringEditMode === 'FromThisOne') {
            indicator.textContent = 'This & Future';
            indicator.style.background = '#fffbeb';
            indicator.style.color = '#f59e0b';
            indicator.style.borderColor = '#fef3c7';
            dateInput.disabled = true;
        } else if (recurringEditMode === 'AllInSeries') {
            const startDate = new Date(expense.recurringStartDate || expense.date);
            indicator.textContent = `Full Series (Started ${startDate.toLocaleDateString('en-US', options)})`;
            indicator.style.background = '#eff6ff';
            indicator.style.color = '#3b82f6';
            indicator.style.borderColor = '#dbeafe';
            dateInput.disabled = true;
        } else {
            // Default/Fallback
            indicator.style.display = 'none';
        }
    } else {
        indicator.style.display = 'none';
    }

    if (expense) {
        document.getElementById('expenseId').value = expense.id;
        document.getElementById('expenseDate').value = expense.date.split('T')[0];
        document.getElementById('expenseAmount').value = expense.amount;
        document.getElementById('expenseDescription').value = expense.description;
        document.getElementById('expenseLayer').value = expense.layerId || '';
        
        // Handle type selection
        document.getElementById('expenseType').value = expense.type || 'Debit';
        
        // Handle color selection
        const color = expense.color || '#efebe9';
        document.getElementById('expenseColor').value = color;
        document.querySelectorAll('.color-square').forEach(c => {
            if (c.dataset.color === color) c.classList.add('selected');
            else c.classList.remove('selected');
        });

        document.getElementById('isRecurring').checked = expense.isRecurring || false;
        document.getElementById('isRecurringInstance').value = expense.parentRecurringItemId ? 'true' : 'false';
        if (expense.isRecurring) {
            document.getElementById('recurringInterval').value = expense.recurringInterval || 1;
            document.getElementById('recurringPeriod').value = expense.recurringPeriod || 'days';
            document.getElementById('recurringOptionsInline').style.display = 'flex';
        } else {
            document.getElementById('recurringOptionsInline').style.display = 'none';
        }
        deleteBtn.style.display = 'inline-block';
    } else {
        document.getElementById('expenseId').value = '';
        document.getElementById('isRecurringInstance').value = 'false';
        document.getElementById('expenseType').value = 'Debit';
        
        const defaultColor = '#efebe9';
        document.getElementById('expenseColor').value = defaultColor;
        document.querySelectorAll('.color-square').forEach(c => {
            if (c.dataset.color === defaultColor) c.classList.add('selected');
            else c.classList.remove('selected');
        });

        if (defaultDate) {
            const dateStr = defaultDate.toISOString().split('T')[0];
            document.getElementById('expenseDate').value = dateStr;
        }
        document.getElementById('isRecurring').checked = false;
        document.getElementById('recurringOptionsInline').style.display = 'none';
        deleteBtn.style.display = 'none';
    }

    modal.style.display = 'block';
}

function showRecurringEditDialog() {
    const modal = document.getElementById('recurringEditModal');
    modal.style.display = 'block';
    
    document.getElementById('editThisOne').onclick = () => {
        recurringEditMode = 'ThisOne';
        modal.style.display = 'none';
        openExpenseForm(currentExpense);
    };
    
    document.getElementById('editFromThisOne').onclick = () => {
        recurringEditMode = 'FromThisOne';
        modal.style.display = 'none';
        openExpenseForm(currentExpense);
    };
    
    document.getElementById('editAllInSeries').onclick = () => {
        recurringEditMode = 'AllInSeries';
        modal.style.display = 'none';
        openExpenseForm(currentExpense);
    };
    
    document.getElementById('cancelRecurringEdit').onclick = () => {
        modal.style.display = 'none';
        currentExpense = null;
        recurringEditMode = null;
    };
}

function closeModal() {
    document.getElementById('expenseModal').style.display = 'none';
}

function toggleRecurringOptions() {
    const isRecurring = document.getElementById('isRecurring').checked;
    document.getElementById('recurringOptionsInline').style.display = isRecurring ? 'flex' : 'none';
}

function saveExpense(e) {
    e.preventDefault();

    const expenseId = document.getElementById('expenseId').value;
    const isRecurring = document.getElementById('isRecurring').checked;
    const expenseDate = document.getElementById('expenseDate').value;
    const isRecurringInstance = document.getElementById('isRecurringInstance').value === 'true';

    const expense = {
        id: expenseId ? parseInt(expenseId) : 0,
        accountId: currentAccountId,
        date: expenseDate,
        amount: parseFloat(document.getElementById('expenseAmount').value),
        description: document.getElementById('expenseDescription').value,
        type: document.getElementById('expenseType').value,
        color: document.getElementById('expenseColor').value,
        layerId: document.getElementById('expenseLayer').value ? parseInt(document.getElementById('expenseLayer').value) : null,
        isRecurring: isRecurring,
        recurringInterval: isRecurring ? parseInt(document.getElementById('recurringInterval').value) : null,
        recurringPeriod: isRecurring ? document.getElementById('recurringPeriod').value : null,
        recurringStartDate: isRecurring ? expenseDate : null,
        recurringEditMode: isRecurring ? recurringEditMode : null
    };

    // If date is disabled, use the original date from the form value (which remains correct)
    // but ensured it's included in the object sent to server.
    if (document.getElementById('expenseDate').disabled && currentExpense) {
        expense.date = currentExpense.date.split('T')[0];
    }

    let url = expenseId ? '/Expense/Update' : '/Expense/Create';

    // If editing a recurring item with a mode, use the special endpoint
    if (expenseId && isRecurring && recurringEditMode) {
        url = `/Expense/UpdateRecurring?mode=${recurringEditMode}`;
    }

    const method = expenseId ? 'PUT' : 'POST';

    fetch(url, {
        method: method,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(expense)
    })
    .then(response => response.json())
    .then(() => {
        closeModal();
        recurringEditMode = null;
        currentExpense = null;
        saveToLocalStorage();
        loadExpenses();
    });
}

function deleteExpense() {
    const expenseId = document.getElementById('expenseId').value;
    const isRecurring = document.getElementById('isRecurring').checked;
    
    if (!expenseId) return;
    
    // If it's a recurring item, show the delete mode dialog
    if (isRecurring) {
        showRecurringDeleteDialog(expenseId);
        return;
    }
    
    // Otherwise, just confirm and delete
    if (!confirm('Are you sure you want to delete this item?')) return;

    fetch(`/Expense/Delete?id=${expenseId}&accountId=${currentAccountId}`, { method: 'DELETE' })
        .then(() => {
            closeModal();
            saveToLocalStorage();
            loadExpenses();
        });
}

function showRecurringDeleteDialog(expenseId) {
    const modal = document.getElementById('recurringDeleteModal');
    const expenseModal = document.getElementById('expenseModal');
    expenseModal.style.display = 'none';
    modal.style.display = 'block';
    
    document.getElementById('deleteThisOne').onclick = () => {
        performRecurringDelete(expenseId, 'ThisOne');
    };
    
    document.getElementById('deleteFromThisOne').onclick = () => {
        performRecurringDelete(expenseId, 'FromThisOne');
    };
    
    document.getElementById('deleteAllInSeries').onclick = () => {
        performRecurringDelete(expenseId, 'AllInSeries');
    };
    
    document.getElementById('cancelRecurringDelete').onclick = () => {
        modal.style.display = 'none';
        expenseModal.style.display = 'block';
    };
}

function performRecurringDelete(expenseId, mode) {
    const expense = currentExpense || expenses.find(e => e.id === parseInt(expenseId));
    
    fetch(`/Expense/DeleteRecurring?id=${expenseId}&accountId=${currentAccountId}&mode=${mode}&date=${expense.date}`, { 
        method: 'DELETE' 
    })
    .then(() => {
        document.getElementById('recurringDeleteModal').style.display = 'none';
        closeModal();
        currentExpense = null;
        saveToLocalStorage();
        loadExpenses();
    });
}

const colorTextMap = {
    '#4caf50': '#1b5e20', // Green -> Dark Green
    '#f44336': '#c62828', // Red -> Dark Red
    '#00a884': '#00695c', // Tealish -> Dark Teal
    '#2196f3': '#1565c0', // Blue -> Dark Blue
    '#ff9800': '#e65100', // Orange -> Dark Orange
    '#9c27b0': '#6a1b9a', // Purple -> Dark Purple
    '#ffeb3b': '#f57f17', // Yellow -> Dark Yellow/Orange
    '#00bcd4': '#00838f', // Cyan -> Dark Cyan
    '#9e9e9e': '#424242', // Grey -> Dark Grey
    '#ffcc80': '#ef6c00', // Light Orange -> Dark Orange
    '#8d3d3d': '#4e2727', // Maroon -> Dark Maroon
    '#efebe9': '#4e342e'  // Light Grey -> Dark Brown
};

const softColorMap = {
    '#4caf50': '#e8f5e9', // Green
    '#f44336': '#ffebee', // Red
    '#00a884': '#e0f2f1', // Tealish
    '#2196f3': '#e3f2fd', // Blue
    '#ff9800': '#fff3e0', // Orange
    '#9c27b0': '#f3e5f5', // Purple
    '#ffeb3b': '#fffde7', // Yellow
    '#00bcd4': '#e0f7fa', // Cyan
    '#9e9e9e': '#f5f5f5', // Grey
    '#ffcc80': '#fff8e1', // Light Orange
    '#8d3d3d': '#efebe9', // Maroon
    '#efebe9': '#fafafa'  // Light Grey
};

function getContrastColor(hexColor) {
    return colorTextMap[hexColor.toLowerCase()] || '#000000';
}

function getSoftColor(hexColor) {
    return softColorMap[hexColor.toLowerCase()] || hexColor;
}

function clearAllData() {
    if (!confirm('Are you sure you want to delete ALL items and layers? This cannot be undone.')) {
        return;
    }

    fetch('/Expense/ClearAll', { method: 'POST' })
        .then(response => response.json())
        .then(() => {
            localStorage.removeItem(STORAGE_KEY);
            expenses = [];
            layers = [];
            dailyBalances = {};
            loadExpenses();
        })
        .catch(err => {
            console.error('Error clearing data:', err);
            alert('Failed to clear data. Please try again.');
        });
}

function openLayerModal(layer = null) {
    if (layer) {
        document.getElementById('layerModal').querySelector('h3').textContent = 'Edit Layer';
        document.getElementById('layerModal').querySelector('.modal-subtitle').textContent = 'Update the name of this layer';
        document.getElementById('layerName').value = layer.name;
        document.getElementById('editLayerId').value = layer.id;
        document.getElementById('saveLayerBtn').textContent = 'UPDATE LAYER';
    } else {
        document.getElementById('layerModal').querySelector('h3').textContent = 'Add New Layer';
        document.getElementById('layerModal').querySelector('.modal-subtitle').textContent = 'Create a conceptual grouping for items';
        document.getElementById('layerName').value = '';
        document.getElementById('editLayerId').value = '';
        document.getElementById('saveLayerBtn').textContent = 'CREATE LAYER';
    }
    document.getElementById('layerModal').style.display = 'block';
}

function closeLayerModal() {
    document.getElementById('layerModal').style.display = 'none';
}

function createLayer(e) {
    e.preventDefault();
    const name = document.getElementById('layerName').value;
    const editId = document.getElementById('editLayerId').value;

    if (editId) {
        fetch('/Expense/UpdateLayer', {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ id: parseInt(editId), accountId: currentAccountId, name: name })
        })
        .then(response => response.json())
        .then(() => {
            closeLayerModal();
            saveToLocalStorage();
            loadExpenses();
        });
    } else {
        fetch('/Expense/CreateLayer', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ accountId: currentAccountId, name: name, isActive: true })
        })
        .then(response => response.json())
        .then(() => {
            closeLayerModal();
            saveToLocalStorage();
            loadExpenses();
        });
    }
}

function renderLayers() {
    const list = document.getElementById('layersList');
    list.innerHTML = '';
    
    // Add "Default" layer
    const defaultItem = document.createElement('div');
    defaultItem.className = `layer-item ${defaultLayerActive ? 'active' : ''} default-layer`;
    defaultItem.innerHTML = `
        <div class="layer-toggle"></div>
        <span class="layer-name">Default</span>
    `;
    defaultItem.addEventListener('click', () => {
        defaultLayerActive = !defaultLayerActive;
        saveToLocalStorage();
        loadExpenses();
    });
    list.appendChild(defaultItem);

    layers.forEach(layer => {
        const item = document.createElement('div');
        item.className = `layer-item ${layer.isActive ? 'active' : ''}`;
        
        item.innerHTML = `
            <div class="layer-toggle"></div>
            <span class="layer-name">${layer.name}</span>
            <div class="layer-actions">
                <button class="btn-edit-layer" title="Edit Layer">✎</button>
                <button class="btn-delete-layer" title="Delete Layer">&times;</button>
            </div>
        `;
        
        item.addEventListener('click', (e) => {
            const deleteBtn = e.target.closest('.btn-delete-layer');
            const editBtn = e.target.closest('.btn-edit-layer');
            
            if (deleteBtn) {
                e.stopPropagation();
                deleteLayer(layer.id);
            } else if (editBtn) {
                e.stopPropagation();
                openLayerModal(layer);
            } else {
                toggleLayer(layer.id);
            }
        });
        
        list.appendChild(item);
    });
}

function toggleLayer(id) {
    fetch(`/Expense/ToggleLayer?id=${id}&accountId=${currentAccountId}`, { method: 'POST' })
    .then(() => {
        saveToLocalStorage();
        loadExpenses();
    });
}

function deleteLayer(id) {
    if (!confirm('Are you sure you want to delete this layer? Items in this layer will be removed.')) return;
    
    fetch(`/Expense/DeleteLayer?id=${id}&accountId=${currentAccountId}`, { method: 'DELETE' })
    .then(() => {
        saveToLocalStorage();
        loadExpenses();
    });
}

function populateLayerDropdown() {
    const dropdown = document.getElementById('expenseLayer');
    const currentValue = dropdown.value;
    
    dropdown.innerHTML = '<option value="">Default</option>';
    layers.forEach(layer => {
        const option = document.createElement('option');
        option.value = layer.id;
        option.textContent = layer.name;
        dropdown.appendChild(option);
    });
    
    dropdown.value = currentValue;
}
