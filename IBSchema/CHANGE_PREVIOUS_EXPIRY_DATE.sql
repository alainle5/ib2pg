create procedure change_previous_expiry_date (
    newdt date)
as
DECLARE VARIABLE old_effective_date DATE;
begin
  select max(effective_date)
    from apportionment_mst
   where effective_date <= :newdt
  into :old_effective_date;

    update apportionment_mst
       set expiry_date = :newdt
       where effective_date = :old_effective_date;
end