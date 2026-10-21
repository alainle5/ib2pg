CREATE OR REPLACE FUNCTION batch_payment_detail_ins(pay_batch_no integer, batch_no integer, invoice_no integer, amount_due numeric(15,2), amount_paid numeric(15,2), check_no text, bill_to_type text, bill_to text, policy_id integer, sysdate date, cancel_date date, policy_no text, invoice_date date, bill_to_first_name text, check_date date, bp_name text, temp_last_name text, bill_to_last_name text, receipt_number text, batch_payment_type text, payment_source text)
RETURNS void
LANGUAGE plpgsql
AS $$
DECLARE
BEGIN
    insert into batch_payment_detail ( pay_batch_no , batch_no , invoice_no , amount_due , amount_paid , check_no , bill_to_type , bill_to , policy_id , sysdate , cancel_date , batch_detail_no , policy_no , invoice_date , bill_to_first_name , check_date , bp_name , temp_last_name , bill_to_last_name , receipt_number , batch_payment_type , payment_source ) values ( pay_batch_no , batch_no , invoice_no , amount_due , amount_paid , check_no , bill_to_type , bill_to , policy_id , sysdate , cancel_date , gen_id ( batch_pmtd_gen , 1 ) , policy_no , invoice_date , bill_to_first_name , check_date , bp_name , temp_last_name , bill_to_last_name , receipt_number , batch_payment_type , payment_source );
END;
$$;
