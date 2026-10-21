CREATE OR REPLACE FUNCTION cofr_additions(ipolicyid integer, ibatchno integer)
RETURNS void
LANGUAGE plpgsql
AS $$
DECLARE
BEGIN
    insert into print_queue ( queue_id , queue_section , queue_type , policy_id , batch_no , origin_id , queue_date , printed_date ) select gen_id ( queue_id_gen , 1 ) , 'COFR' , 'ADDED' , v . policy_id , v . batch_no , v . origin_id , 'NOW' , null from vessels v where v . policy_id = ipolicyid and v . batch_no = ibatchno and v . cofr = 1 and v . status in ( '' , 'H' ) and not exists ( select o . unique_id from vessels o where o . policy_id = v . policy_id and o . item_no = v . item_no and o . batch_no < v . batch_no ) and not exists ( select q . queue_id from print_queue q where q . policy_id = v . policy_id and q . batch_no = v . batch_no and q . origin_id = v . origin_id and q . printed_date is null );
    insert into print_queue ( queue_id , queue_section , queue_type , policy_id , batch_no , origin_id , queue_date , printed_date ) select gen_id ( queue_id_gen , 1 ) , 'COFR' , 'ADDED' , v . policy_id , v . batch_no , v . origin_id , 'NOW' , null from vessels v , vessels o where v . policy_id = ipolicyid and v . batch_no = ibatchno and v . cofr = 1 and v . status in ( '' , 'H' ) and o . policy_id = v . policy_id and o . item_no = v . item_no and o . cofr = 0 and o . status in ( '' , 'H' ) and o . batch_no = ( select max ( b . batch_no ) from vessels b where b . policy_id = v . policy_id and b . item_no = v . item_no and b . batch_no < v . batch_no ) and not exists ( select q . queue_id from print_queue q where q . policy_id = v . policy_id and q . batch_no = v . batch_no and q . origin_id = v . origin_id and q . printed_date is null );
END;
$$;
