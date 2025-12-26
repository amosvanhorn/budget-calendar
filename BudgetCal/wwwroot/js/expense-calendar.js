let currentYear = new Date().getFullYear();
let currentMonth = new Date().getMonth() + 1;
let expenses = [];
let dailyBalances = {};

document.addEventListener('DOMContentLoaded', function() {
    loadExpenses();

    document.getElementById('prevMonth').addEventListener('click', () => navigateMonth(-1));
    document.getElementById('nextMonth').addEventListener('click', () => navigateMonth(1));
    document.getElementById('addExpenseBtn').addEventListener('click', () => openModal());
    document.querySelector('.close').addEventListener('click', closeModal);
    document.getElementById('expenseForm').addEventListener('submit', saveExpense);
    document.getElementById('deleteExpenseBtn').addEventListener('click', deleteExpense);
    document.getElementById('isRecurring').addEventListener('change', toggleRecurringOptions);

    window.addEventListener('click', (e) => {
        if (e.target === document.getElementById('expenseModal')) {
            closeModal();
        }
    });
});

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

    const dayNumber = document.createElement('div');
    dayNumber.className = 'day-number';
    dayNumber.textContent = day;
    cell.appendChild(dayNumber);

    const expensesContainer = document.createElement('div');
    expensesContainer.className = 'expenses-container';

    const dateStr = `${currentYear}-${String(currentMonth).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
    const dayExpenses = expenses.filter(e => e.date.startsWith(dateStr));

    let totalAmount = 0;
    dayExpenses.forEach(expense => {
        totalAmount += expense.amount;
        const expenseItem = document.createElement('div');
        expenseItem.className = 'expense-item';
        expenseItem.innerHTML = `
            <span class="expense-category">${expense.category}</span>
            <span class="expense-amount">$${expense.amount.toFixed(2)}</span>
        `;
        expenseItem.addEventListener('click', (e) => {
            e.stopPropagation();
            openModal(expense);
        });
        expensesContainer.appendChild(expenseItem);
    });

    if (totalAmount > 0) {
        const totalDiv = document.createElement('div');
        totalDiv.className = 'day-total';
        totalDiv.textContent = `Total: $${totalAmount.toFixed(2)}`;
        expensesContainer.appendChild(totalDiv);
    }

    // Add balance display
    const balance = dailyBalances[dateStr];

    if (balance !== undefined && balance !== 0) {
        const balanceDiv = document.createElement('div');
        balanceDiv.className = 'day-balance';
        if (balance < 0) {
            balanceDiv.classList.add('negative');
        }
        balanceDiv.textContent = `Balance: $${balance.toFixed(2)}`;
        expensesContainer.appendChild(balanceDiv);
    }

    cell.appendChild(expensesContainer);

    cell.addEventListener('click', () => {
        openModal(null, new Date(currentYear, currentMonth - 1, day));
    });

    return cell;
}

function openModal(expense = null, defaultDate = null) {
    const modal = document.getElementById('expenseModal');
    const form = document.getElementById('expenseForm');
    const deleteBtn = document.getElementById('deleteExpenseBtn');

    form.reset();

    if (expense) {
        document.getElementById('modalTitle').textContent = 'Edit Expense';
        document.getElementById('expenseId').value = expense.id;
        document.getElementById('expenseDate').value = expense.date.split('T')[0];
        document.getElementById('expenseAmount').value = expense.amount;
        document.getElementById('expenseDescription').value = expense.description;
        document.getElementById('expenseCategory').value = expense.category;
        document.getElementById('isRecurring').checked = expense.isRecurring || false;
        if (expense.isRecurring) {
            document.getElementById('recurringInterval').value = expense.recurringInterval || 1;
            document.getElementById('recurringPeriod').value = expense.recurringPeriod || 'days';
            document.getElementById('recurringOptions').style.display = 'block';
        } else {
            document.getElementById('recurringOptions').style.display = 'none';
        }
        deleteBtn.style.display = 'inline-block';
    } else {
        document.getElementById('modalTitle').textContent = 'Add Expense';
        document.getElementById('expenseId').value = '';
        if (defaultDate) {
            const dateStr = defaultDate.toISOString().split('T')[0];
            document.getElementById('expenseDate').value = dateStr;
        }
        document.getElementById('isRecurring').checked = false;
        document.getElementById('recurringOptions').style.display = 'none';
        deleteBtn.style.display = 'none';
    }

    modal.style.display = 'block';
}

function closeModal() {
    document.getElementById('expenseModal').style.display = 'none';
}

function toggleRecurringOptions() {
    const isRecurring = document.getElementById('isRecurring').checked;
    document.getElementById('recurringOptions').style.display = isRecurring ? 'block' : 'none';
}

function saveExpense(e) {
    e.preventDefault();

    const expenseId = document.getElementById('expenseId').value;
    const isRecurring = document.getElementById('isRecurring').checked;
    const expenseDate = document.getElementById('expenseDate').value;
    
    const expense = {
        id: expenseId ? parseInt(expenseId) : 0,
        date: expenseDate,
        amount: parseFloat(document.getElementById('expenseAmount').value),
        description: document.getElementById('expenseDescription').value,
        category: document.getElementById('expenseCategory').value,
        isRecurring: isRecurring,
        recurringInterval: isRecurring ? parseInt(document.getElementById('recurringInterval').value) : null,
        recurringPeriod: isRecurring ? document.getElementById('recurringPeriod').value : null,
        recurringStartDate: isRecurring ? expenseDate : null
    };

    const url = expenseId ? '/Expense/Update' : '/Expense/Create';
    const method = expenseId ? 'PUT' : 'POST';

    fetch(url, {
        method: method,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(expense)
    })
    .then(response => response.json())
    .then(() => {
        closeModal();
        loadExpenses();
    });
}

function deleteExpense() {
    const expenseId = document.getElementById('expenseId').value;
    if (!expenseId || !confirm('Are you sure you want to delete this expense?')) return;

    fetch(`/Expense/Delete?id=${expenseId}`, { method: 'DELETE' })
        .then(() => {
            closeModal();
            loadExpenses();
        });
}
