CREATE OR REPLACE FUNCTION als_tempfile(policyid integer, batchno integer, sessionid integer)
RETURNS void
LANGUAGE plpgsql
AS $$
DECLARE
    vlocno integer;
    vtype text;
    vdesc text;
    vprimary text;
    vcova integer;
    vcovb integer;
    vcovc integer;
    vcovd integer;
    vitemamt integer;
    vform text;
    vreplcost text;
    vprem text;
    vunique integer;
    vpolid integer;
    vbno integer;
    vdummy integer;
    vitem_no integer;
    ipolicyid integer;
    iitemno integer;
    ieffdate date;
    vmodifier text;
    nwrittenprem numeric(15,2);
BEGIN
    FOR vlocno, vitem_no, vtype, vdesc, vprimary, vcova, vcovb, vcovc, vcovd, vform, vreplcost, vprem, vunique, ieffdate, nwrittenprem IN SELECT d.location_no, d.dwelling_no, 'D', d.descr, d.primary_dwelling, d.cova, d.covb, d.covc, d.covd, form.ref_desc, d.rcc_desc, d.annual_premium, d.unique_id, d.effective_date, d.writtenprem FROM vdwelling d, ref_table form WHERE d.policy_id = policyid and d.batch_no = batchno and d.policyform_id = form.reftable_id order by location_no asc, primary_dwelling desc, dwelling_no asc LOOP
        IF vreplcost <> '' THEN
            vreplcost := 'RC';
        ELSE
            vreplcost := 'ACV';
        END IF;
        insert into dwell_items_loc ( locno , item_no , item_type , item_descr , dwell_primary , cova_amt , covb_amt , covc_amt , covd_amt , item_form , item_repl_cost , item_premium , unique_id , session_id , writtenprem ) values ( vlocno , vitem_no , vtype , vdesc , vprimary , vcova , vcovb , vcovc , vcovd , vform , vreplcost , vprem , vunique , sessionid , nwrittenprem );
        FOR vdesc, vitemamt, vprem, vmodifier IN SELECT rt.ref_desc, o."limit", pa.annual_premium, rt.modifier_key FROM options o, premium_audit pa, ref_table rt WHERE o.policy_id = policyid and o.item_no = vitem_no and o.reftable_id = rt.reftable_id and o.effective_date <= ieffdate and o.expiry_date >= ieffdate and (o.status = '' or o.status = 'H') and (o.endorsement = 'FO-061' or o.endorsement = 'FO-170') and o.policy_id = pa.policy_id and o.batch_no = pa.batch_no and ((o.option_type = pa.origin_type) or (o.option_type = 'AO' and pa.origin_type = 'D')) and o.origin_id = pa.origin and o.option_code = rt.ref_code and o.state = rt.state order by o.endorsement asc LOOP
            insert into dwell_items_loc ( locno , item_no , item_type , item_descr , item_amt , item_premium , unique_id , session_id , options , modifier , writtenprem ) values ( vlocno , vitem_no , vtype , vdesc , vitemamt , vprem , vunique , sessionid , 'Y' , vmodifier , nwrittenprem );
        END LOOP;
    END LOOP;
    FOR vlocno, vtype, vdesc, vitemamt, vform, vreplcost, vprem, vunique, ipolicyid, iitemno, ieffdate IN SELECT i.location_no, i.cov_type, i.descr, i."limit", 'Broad', 'ACV', i.annual_premium, i.unique_id, i.policy_id, i.item_no, i.effective_date FROM vitems i, ref_table loc WHERE i.policy_id = policyid and i.batch_no = batchno and i.reftable_id = loc.reftable_id and loc.location = 1 order by i.location_no asc, i.cov_type desc LOOP
        FOR vdummy IN SELECT o.policy_id FROM options o WHERE o.policy_id = ipolicyid and o.item_no = iitemno and o.effective_date <= ieffdate and o.expiry_date >= ieffdate and (o.status = '' or o.status = 'H') and (o.option_code = 'SFBARNS' or o.option_code = 'FMACH') LOOP
            vform := 'Special';
        END LOOP;
        FOR vdummy IN SELECT o.policy_id FROM options o WHERE o.policy_id = ipolicyid and o.item_no = iitemno and o.effective_date <= ieffdate and o.expiry_date >= ieffdate and (o.status = '' or o.status = 'H') and o.option_code = 'FREPLACE' LOOP
            vreplcost := 'RC';
        END LOOP;
        insert into dwell_items_loc ( locno , item_no , item_type , item_descr , item_amt , item_form , item_repl_cost , item_premium , unique_id , session_id , writtenprem ) values ( vlocno , iitemno , vtype , vdesc , vitemamt , vform , vreplcost , vprem , vunique , sessionid , nwrittenprem );
    END LOOP;
END;
$$;
