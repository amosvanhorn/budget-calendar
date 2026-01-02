const { getContrastColor, getSoftColor, updateMonth, formatDate, calculateDailyBalances } = require('../expense-calendar');

describe('Expense Calendar Logic', () => {
    describe('getContrastColor', () => {
        test('should return dark green for green hex', () => {
            expect(getContrastColor('#4caf50')).toBe('#1b5e20');
        });

        test('should return dark red for red hex', () => {
            expect(getContrastColor('#f44336')).toBe('#c62828');
        });

        test('should return dark teal for tealish hex', () => {
            expect(getContrastColor('#00a884')).toBe('#00695c');
        });

        test('should return dark orange for orange hex', () => {
            expect(getContrastColor('#ff9800')).toBe('#e65100');
        });

        test('should return black for unknown color', () => {
            expect(getContrastColor('#ffffff')).toBe('#000000');
        });

        test('should be case insensitive', () => {
            expect(getContrastColor('#4CAF50')).toBe('#1b5e20');
        });
    });

    describe('getSoftColor', () => {
        test('should return light green for green hex', () => {
            expect(getSoftColor('#4caf50')).toBe('#e8f5e9');
        });

        test('should return light red for red hex', () => {
            expect(getSoftColor('#f44336')).toBe('#ffebee');
        });

        test('should return same color if not in map', () => {
            expect(getSoftColor('#123456')).toBe('#123456');
        });
    });

    describe('updateMonth', () => {
        test('should increment month within the same year', () => {
            const result = updateMonth(2023, 1, 1);
            expect(result).toEqual({ year: 2023, month: 2 });
        });

        test('should decrement month within the same year', () => {
            const result = updateMonth(2023, 2, -1);
            expect(result).toEqual({ year: 2023, month: 1 });
        });

        test('should roll over to next year when incrementing past December', () => {
            const result = updateMonth(2023, 12, 1);
            expect(result).toEqual({ year: 2024, month: 1 });
        });

        test('should roll back to previous year when decrementing past January', () => {
            const result = updateMonth(2023, 1, -1);
            expect(result).toEqual({ year: 2022, month: 12 });
        });
    });

    describe('formatDate', () => {
        test('should format date with leading zeros for month and day', () => {
            expect(formatDate(2023, 1, 5)).toBe('2023-01-05');
        });

        test('should format date without extra zeros when not needed', () => {
            expect(formatDate(2023, 11, 25)).toBe('2023-11-25');
        });
    });

    describe('calculateDailyBalances', () => {
        test('should correctly accumulate debits and credits', () => {
            const startingBalance = 1000;
            const expenses = [
                { date: '2023-01-01T00:00:00', amount: 100, type: 'Debit' },
                { date: '2023-01-02T00:00:00', amount: 50, type: 'Credit' },
                { date: '2023-01-03T00:00:00', amount: 200, type: 'Debit' }
            ];
            
            const result = calculateDailyBalances(startingBalance, '2023-01-01', expenses);
            
            expect(result['2023-01-01']).toBe(900);
            expect(result['2023-01-02']).toBe(950);
            expect(result['2023-01-03']).toBe(750);
        });

        test('should handle multiple expenses on the same day', () => {
            const startingBalance = 1000;
            const expenses = [
                { date: '2023-01-01T08:00:00', amount: 100, type: 'Debit' },
                { date: '2023-01-01T12:00:00', amount: 200, type: 'Debit' }
            ];
            
            const result = calculateDailyBalances(startingBalance, '2023-01-01', expenses);
            
            // The logic currently updates the balance for each expense.
            // If they are on the same day, the LAST one's balance will be stored in that day's key.
            expect(result['2023-01-01']).toBe(700);
        });

        test('should sort expenses by date internally', () => {
            const startingBalance = 1000;
            const expenses = [
                { date: '2023-01-03T00:00:00', amount: 200, type: 'Debit' },
                { date: '2023-01-01T00:00:00', amount: 100, type: 'Debit' },
                { date: '2023-01-02T00:00:00', amount: 50, type: 'Credit' }
            ];
            
            const result = calculateDailyBalances(startingBalance, '2023-01-01', expenses);
            
            expect(result['2023-01-01']).toBe(900);
            expect(result['2023-01-02']).toBe(950);
            expect(result['2023-01-03']).toBe(750);
        });

        test('should handle empty expenses list', () => {
            const result = calculateDailyBalances(1000, '2023-01-01', []);
            expect(result).toEqual({});
        });

        test('should treat unknown expense types as Credit (addition)', () => {
            const startingBalance = 1000;
            const expenses = [
                { date: '2023-01-01T00:00:00', amount: 100, type: 'Unknown' }
            ];
            
            const result = calculateDailyBalances(startingBalance, '2023-01-01', expenses);
            expect(result['2023-01-01']).toBe(1100);
        });

        test('should handle zero amount expenses', () => {
            const startingBalance = 1000;
            const expenses = [
                { date: '2023-01-01T00:00:00', amount: 0, type: 'Debit' }
            ];
            
            const result = calculateDailyBalances(startingBalance, '2023-01-01', expenses);
            expect(result['2023-01-01']).toBe(1000);
        });
    });
});
