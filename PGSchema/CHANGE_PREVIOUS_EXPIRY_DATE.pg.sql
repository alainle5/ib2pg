CREATE OR REPLACE FUNCTION change_previous_expiry_date(newdt date)
RETURNS void
LANGUAGE plpgsql
AS $$
DECLARE
    old_effective_date date;
BEGIN
    SELECT max (effective_date) INTO old_effective_date FROM apportionment_mst WHERE effective_date <= newdt;
    update apportionment_mst set expiry_date = newdt where effective_date = old_effective_date;
END;
$$;
