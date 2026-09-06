IF COL_LENGTH('BudgetPlanItems','FromDate') IS NULL ALTER TABLE BudgetPlanItems ADD FromDate date NULL;
IF COL_LENGTH('BudgetPlanItems','ToDate') IS NULL ALTER TABLE BudgetPlanItems ADD ToDate date NULL;
