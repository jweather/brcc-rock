-- PII scrubbing for RockRMS
-- contact jweather@blue-ridge.org


-- replace giving with random numbers between $0 and $100
update FinancialTransactionDetail set [Amount] = ABS(CHECKSUM(newid()))%100;

-- randomize giving date/time
update FinancialTransaction set [CreatedDateTime] = DATEADD(day, (ABS(CHECKSUM(NEWID())) % 65530), 0);
update FinancialTransaction set [TransactionDateTime] = DATEADD(day, (ABS(CHECKSUM(NEWID())) % 65530), 0);
update FinancialTransactionDetail set [CreatedDateTime] = DATEADD(day, (ABS(CHECKSUM(NEWID())) % 65530), 0);
update FinancialTransaction set [TransactionCode] = ABS(CHECKSUM(newid()))%10000;

-- replace pledges with random numbers between $0 and $100
update FinancialPledge set [TotalAmount] = ABS(CHECKSUM(newid()))%100;

-- replace e-mail with [First][Last]@brcc.test
update Person set [Email] = [FirstName]+[LastName]+'@brcc.test';

-- set everyone's address to RockRMS HQ
update GroupLocation SET LocationId=1
  FROM GroupLocation gl INNER JOIN [Group] g ON gl.GroupId=g.Id
  WHERE g.GroupTypeId=10; -- families
  
-- randomize phone numbers
update PhoneNumber set [Number] = 4340000000 + ABS(CHECKSUM(newid()))%10000000;

-- randomize birthdates
update Person set [BirthDay] = 1 + ABS(CHECKSUM(newid()))%27;
update Person set [BirthMonth] = 1 + ABS(CHECKSUM(newid()))%11;
update Person set [BirthYear] = 1900 + ABS(CHECKSUM(newid()))%100;
