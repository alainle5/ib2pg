CREATE OR REPLACE FUNCTION batch_payment_master_ins(no_of_checks integer, total_amount_of_checks numeric(15,2), batch_type text, user_key integer, description text, batch_payment_type text)
RETURNS TABLE(pay_batch_no integer)
LANGUAGE plpgsql
AS $$
DECLARE
BEGIN
    pay_batch_no := gen_id(batch_pmt_gen, 1);
    insert into batch_payment_master ( pay_batch_no , no_of_checks , total_amount_of_checks , posted , last_modified , sysdate , batch_type , user_key , description ) values ( pay_batch_no , no_of_checks , total_amount_of_checks , 'N' , 'NOW' , 'NOW' , 'I' , user_key , description );
    RETURN NEXT;
END;
$$;
