ALTER TABLE accounts ADD COLUMN name_zh varchar(50);

INSERT INTO accounts (name, name_zh, type) VALUES

-- ASSETS 資產 (5)
('Cash',                       '現金',           'asset'),
('Bank Account',               '銀行存款',       'asset'),
('Loan Receivable',            '應收借款',       'asset'),
('Real Estate',                '不動產',         'asset'),
('Vehicle',                    '車輛',           'asset'),

-- LIABILITIES 負債 (4)
('Credit Card Payable',        '信用卡應付帳款', 'liability'),
('Mortgage Loan',              '房屋貸款',       'liability'),
('Personal Loan',              '信用貸款',       'liability'),
('Loan Payable (Personal)',    '應付私人借款',   'liability'),

-- EQUITY 權益 (1)
('Opening Balance Equity',     '期初餘額',       'equity'),

-- REVENUE 收入 (5)
('Salary Income',              '薪資收入',       'revenue'),
('Bonus Income',               '獎金收入',       'revenue'),
('Interest Income',            '利息收入',       'revenue'),
('Investment Gain',            '投資利益',       'revenue'),
('Other Income',               '其他收入',       'revenue'),

-- EXPENSES 費用 (12)
('Food & Dining',              '餐飲',          'expense'),
('Rent',                       '房租',          'expense'),
('Utilities',                  '水電瓦斯',       'expense'),
('Internet & Phone',           '網路與電話費',    'expense'),
('Transportation',             '交通',           'expense'),
('Medical & Healthcare',       '醫療保健',       'expense'),
('Insurance Premium',          '保險費',         'expense'),
('Shopping',                   '購物',           'expense'),
('Entertainment & Travel',     '娛樂旅遊',       'expense'),
('Interest Expense',           '利息費用',       'expense'),
('Investment Loss',            '投資損失',       'expense'),
('Other Expense',              '其他支出',       'expense')

ON CONFLICT DO NOTHING;