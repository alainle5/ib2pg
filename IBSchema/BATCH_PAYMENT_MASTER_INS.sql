create procedure batch_payment_master_ins (
    no_of_checks           integer,
    total_amount_of_checks numeric(15,2),
    batch_type             varchar(1) character set none,
    user_key               integer,
    description            varchar(200) character set none,
    batch_payment_type     varchar(10) character set none)
returns (
    pay_batch_no integer)
as
begin
  pay_batch_no = GEN_ID(BATCH_PMT_GEN,1);

  insert into batch_payment_master (
    pay_batch_no,
    no_of_checks,
    total_amount_of_checks,
    posted,
    last_modified,
    sysdate,
    batch_type,
    user_key,
    description)
  values (
    :pay_batch_no,
    :no_of_checks,
    :total_amount_of_checks,
    'N',
    'NOW',
    'NOW',
    'I',
    :user_key,
    :description);

   SUSPEND;
end