const { getContrastColor, getSoftColor } = require('../expense-calendar');

describe('Expense Calendar Logic', () => {
    describe('getContrastColor', () => {
        test('should return dark green for green hex', () => {
            expect(getContrastColor('#4caf50')).toBe('#1b5e20');
        });

        test('should return dark red for red hex', () => {
            expect(getContrastColor('#f44336')).toBe('#c62828');
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

        test('should return same color if not in map', () => {
            expect(getSoftColor('#123456')).toBe('#123456');
        });
    });
});
