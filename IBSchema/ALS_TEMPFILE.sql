create procedure als_tempfile (
    policyid  integer,
    batchno   integer,
    sessionid integer)
as
DECLARE VARIABLE VLOCNO INTEGER;
DECLARE VARIABLE VTYPE CHAR(10);
DECLARE VARIABLE VDESC CHAR(255);
DECLARE VARIABLE VPRIMARY CHAR(2);
DECLARE VARIABLE VCOVA INTEGER;
DECLARE VARIABLE VCOVB INTEGER;
DECLARE VARIABLE VCOVC INTEGER;
DECLARE VARIABLE VCOVD INTEGER;
DECLARE VARIABLE VITEMAMT INTEGER;
DECLARE VARIABLE VFORM CHAR(255);
DECLARE VARIABLE VREPLCOST CHAR(255);
DECLARE VARIABLE VPREM CHAR(80);
DECLARE VARIABLE VUNIQUE INTEGER;
DECLARE VARIABLE VPOLID INTEGER;
DECLARE VARIABLE VBNO INTEGER;
DECLARE VARIABLE VDUMMY INTEGER;
DECLARE VARIABLE VITEM_NO INTEGER;
DECLARE VARIABLE IPOLICYID INTEGER;
DECLARE VARIABLE IITEMNO INTEGER;
DECLARE VARIABLE IEFFDATE DATE;
DECLARE VARIABLE VMODIFIER CHAR(10);
DECLARE VARIABLE NWRITTENPREM NUMERIC(15,2);
BEGIN

  FOR SELECT
    d.location_no, d.dwelling_no, 'D', d.descr, d.primary_dwelling, d.covA, d.covB, d.covC, d.covD,
    form.ref_desc, d.rcc_desc, d.annual_premium, d.unique_id, d.effective_date, d.writtenprem
  FROM
    vDwelling d,
    ref_table form
  WHERE
    d.policy_id     = :PolicyId and
    d.batch_no      = :BatchNo and
    d.PolicyForm_Id = form.reftable_id
  ORDER BY
    location_no asc, primary_dwelling desc, dwelling_no asc
  INTO :vLocno, :vItem_no, :vType, :vDesc, :vPrimary, :vCovA, :vCovB, :vCovC, :vCovD, :vForm, :vReplCost, :vPrem, :vUnique, :iEffDate, :nwrittenprem
  DO
  BEGIN
    IF (:vReplCost <> '') THEN
      vReplCost = 'RC';
    ELSE
      vReplCost = 'ACV';
    insert into DWELL_ITEMS_LOC (locno, item_no, item_type, item_descr, dwell_primary, covA_amt, covB_amt, covC_amt, covD_amt,
                      item_form, item_repl_cost, item_premium, unique_id, session_id, writtenprem)
    values(:vLocno, :vItem_no, :vType, :vDesc, :vPrimary, :vCovA, :vCovB, :vCovC, :vCovD, :vForm, :vReplCost, :vPrem, :vUnique, :SessionId, :nwrittenprem);

    FOR SELECT
      rt.ref_desc, o.limit, pa.annual_premium, rt.modifier_key
    FROM
      options o,
      premium_audit pa,
      ref_table rt
    WHERE
      o.policy_id       = :PolicyId      and
      o.item_no         = :vItem_no      and
      o.reftable_id     = rt.reftable_id and
      o.effective_date <= :iEffDate      and
      o.expiry_date    >= :iEffDate      and
      (o.status = '' or o.status = 'H')  and
      (o.endorsement = 'FO-061' or o.endorsement = 'FO-170') and
      o.policy_id = pa.policy_id and
      o.batch_no = pa.batch_no   and
      ((o.option_type = pa.origin_type) or (o.option_type = 'AO' and pa.origin_type = 'D'))and
      o.origin_id = pa.origin and
      o.option_code = rt.ref_code and
      o.state = rt.state
     ORDER BY
       o.endorsement asc
     INTO
       :vDesc, :vItemAmt, :vPrem, :vModifier
     DO
     BEGIN
       insert into DWELL_ITEMS_LOC (locno, item_no, item_type, item_descr, item_amt, item_premium, unique_id, session_id, options, modifier, writtenprem)
       values(:vLocno, :vItem_no, :vType, :vDesc, :vItemAmt, :vPrem, :vUnique, :SessionId, 'Y', :vModifier, :nwrittenprem);
     END

  END


  FOR SELECT
    i.location_no, i.cov_type, i.descr, i.limit, 'Broad', 'ACV', i.annual_premium, i.unique_id,
    i.policy_id, i.item_no, i.effective_date
  FROM
    vItems i,
    ref_table loc
  WHERE
    i.policy_id   = :PolicyId       and
    i.batch_no    = :BatchNo        and
    i.reftable_id = loc.reftable_id and
    loc.location  = 1
  ORDER BY
    i.location_no asc, i.cov_type desc
  INTO :vLocno, :vType, :vDesc, :vItemAmt, :vForm, :vReplCost, :vPrem, :vUnique, :iPolicyId, :iItemNo, :iEffDate
  DO
  BEGIN

     FOR SELECT
       o.policy_id
     FROM
       options o
     WHERE
       o.policy_id       = :iPolicyId    and
       o.item_no         = :iItemNo      and
       o.effective_date <= :iEffDate     and
       o.expiry_date    >= :iEffDate     and
       (o.status = '' or o.status = 'H') and
       (o.option_code = 'SFBARNS' or
        o.option_code = 'FMACH')
     INTO
       :vDummy
     DO
     BEGIN
       vForm = 'Special';
     END

     FOR SELECT
       o.policy_id
     FROM
       options o
     WHERE
       o.policy_id       = :iPolicyId    and
       o.item_no         = :iItemNo      and
       o.effective_date <= :iEffDate     and
       o.expiry_date    >= :iEffDate     and
       (o.status = '' or o.status = 'H') and
       o.option_code = 'FREPLACE'
     INTO
       :vDummy
     DO
     BEGIN
       vReplCost = 'RC';
     END

    insert into DWELL_ITEMS_LOC (locno, item_no, item_type, item_descr, item_amt, item_form, item_repl_cost, item_premium, unique_id, session_id, writtenprem)
    values(:vLocno, :iItemNo, :vType, :vDesc, :vItemAmt, :vForm, :vReplCost, :vPrem, :vUnique, :SessionId, :nwrittenprem);

  END
END