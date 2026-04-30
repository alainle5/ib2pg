CREATE OR REPLACE FUNCTION process_invoice(invoice_id integer, discount numeric(5,2))
RETURNS TABLE(status_code integer, status_msg varchar(100))
LANGUAGE plpgsql
AS $$
DECLARE
    subtotal numeric(12,2);
    tax numeric(12,2);
    apply integer;
BEGIN
    SELECT amount, tax_rate INTO subtotal, tax FROM invoices WHERE id = invoice_id;
    IF subtotal IS NULL THEN
        status_code := -1;
        status_msg := 'Invoice not found';
        RETURN NEXT;
        RETURN;
    END IF;
    IF discount > 0 THEN
        IF subtotal > 500 THEN
            SELECT apply INTO apply FROM calc_discount(subtotal, discount);
            subtotal := subtotal - apply;
        ELSE
            subtotal := subtotal - subtotal * discount / 100;
        END IF;
    END IF;
    status_code := 0;
    status_msg := 'OK';
    RETURN NEXT;
END;
$$;
