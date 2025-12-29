let currentYear = new Date().getFullYear();
let currentMonth = new Date().getMonth() + 1;
let expenses = [];
let dailyBalances = {};
let currentExpense = null;
let recurringEditMode = null;
let balanceOverrides = {};
let currentBalanceDate = null;
const STORAGE_KEY = 'budget_calendar_data';

document.addEventListener('DOMContentLoaded', function() {
    loadStorageToServer().then(() => {
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
    document.getElementById('cancelBalanceBtn').addEventListener('click', closeBalanceModal);
    document.getElementById('cancelExpenseBtn').addEventListener('click', closeModal);
    document.getElementById('balanceForm').addEventListener('submit', saveBalanceOverride);

    // Color picker logic
    document.querySelectorAll('.color-square').forEach(square => {
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
            return fetch('/Expense/LoadFromStorage', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    items: data.expenses || [],
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
            localStorage.setItem(STORAGE_KEY, JSON.stringify(data));
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

function loadExpenses() {
    Promise.all([
        fetch(`/Expense/GetExpenses?year=${currentYear}&month=${currentMonth}`).then(r => r.json()),
        fetch(`/Expense/GetDailyBalances?year=${currentYear}&month=${currentMonth}`).then(r => r.json())
    ])
    .then(([expensesData, balancesData]) => {
        expenses = expensesData;
        dailyBalances = balancesData;
        updateMonthDisplay();
        renderCalendar();
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
        const bgColor = expense.color || '#e3f2fd';
        expenseItem.style.backgroundColor = bgColor;
        expenseItem.style.color = getContrastColor(bgColor);
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

    if (balanceData !== undefined && balanceData.balance !== 0) {
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
        balanceDiv.textContent = `Balance: $${balance.toFixed(2)}`;
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
    // If editing a recurring instance, show the edit mode dialog first
    if (expense && expense.isRecurring && expense.parentRecurringItemId) {
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

    form.reset();

    // Set header date subtitle
    const dateToDisplay = expense ? new Date(expense.date) : (defaultDate || new Date());
    const options = { month: 'short', day: 'numeric', year: 'numeric' };
    document.getElementById('modalSubtitle').textContent = `Scheduled for ${dateToDisplay.toLocaleDateString('en-US', options)}`;

    if (expense) {
        document.getElementById('expenseId').value = expense.id;
        document.getElementById('expenseDate').value = expense.date.split('T')[0];
        document.getElementById('expenseAmount').value = expense.amount;
        document.getElementById('expenseDescription').value = expense.description;
        
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
        date: expenseDate,
        amount: parseFloat(document.getElementById('expenseAmount').value),
        description: document.getElementById('expenseDescription').value,
        type: document.getElementById('expenseType').value,
        color: document.getElementById('expenseColor').value,
        isRecurring: isRecurring,
        recurringInterval: isRecurring ? parseInt(document.getElementById('recurringInterval').value) : null,
        recurringPeriod: isRecurring ? document.getElementById('recurringPeriod').value : null,
        recurringStartDate: isRecurring ? expenseDate : null,
        recurringEditMode: isRecurringInstance ? recurringEditMode : null
    };

    let url = expenseId ? '/Expense/Update' : '/Expense/Create';

    // If editing a recurring instance with a mode, use the special endpoint
    if (expenseId && isRecurringInstance && recurringEditMode) {
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
    const isRecurringInstance = document.getElementById('isRecurringInstance').value === 'true';
    
    if (!expenseId) return;
    
    // If it's a recurring instance, show the delete mode dialog
    if (isRecurringInstance) {
        showRecurringDeleteDialog(expenseId);
        return;
    }
    
    // Otherwise, just confirm and delete
    if (!confirm('Are you sure you want to delete this item?')) return;

    fetch(`/Expense/Delete?id=${expenseId}`, { method: 'DELETE' })
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
    
    fetch(`/Expense/DeleteRecurring?id=${expenseId}&mode=${mode}&date=${expense.date}`, { 
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
    '#4caf50': '#ffffff', // Green -> White
    '#f44336': '#ffffff', // Red -> White
    '#00a884': '#ffffff', // Tealish -> White
    '#2196f3': '#ffffff', // Blue -> White
    '#ff9800': '#ffffff', // Orange -> White
    '#9c27b0': '#ffffff', // Purple -> White
    '#ffeb3b': '#000000', // Yellow -> Black
    '#00bcd4': '#ffffff', // Cyan -> White
    '#9e9e9e': '#ffffff', // Grey -> White
    '#ffcc80': '#000000', // Light Orange -> Black
    '#8d3d3d': '#ffffff', // Maroon -> White
    '#efebe9': '#000000'  // Light Grey -> Black
};

function getContrastColor(hexColor) {
    return colorTextMap[hexColor.toLowerCase()] || '#000000';
}

function clearAllData() {
    if (!confirm('Are you sure you want to delete ALL items? This cannot be undone.')) {
        return;
    }

    fetch('/Expense/ClearAll', { method: 'POST' })
        .then(response => response.json())
        .then(() => {
            localStorage.removeItem(STORAGE_KEY);
            expenses = [];
            dailyBalances = {};
            loadExpenses();
        })
        .catch(err => {
            console.error('Error clearing data:', err);
            alert('Failed to clear data. Please try again.');
        });
}
