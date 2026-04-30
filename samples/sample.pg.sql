CREATE OR REPLACE FUNCTION get_order_totals(customer_id integer)
RETURNS TABLE(order_id integer, total numeric(12,2))
LANGUAGE plpgsql
AS $$
DECLARE
    cnt integer;
    running numeric(12,2);
BEGIN
    cnt := 0;
    running := 0;
    FOR order_id, total IN SELECT o.order_id, o.amount FROM orders o WHERE o.customer_id = customer_id LOOP
        cnt := cnt + 1;
        running := running + total;
        IF total > 1000 THEN
            PERFORM apply_discount(order_id, 10);
        END IF;
        IF total = 0 THEN
            running := running;
        END IF;
        RETURN NEXT;
    END LOOP;
END;
$$;
