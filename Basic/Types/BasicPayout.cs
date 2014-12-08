using System;
using Swarmops.Basic.Interfaces;

namespace Swarmops.Basic.Types
{
    public class BasicPayout : IHasIdentity
    {
        public BasicPayout (int payoutId, int organizationId, string bank, string account,
            string reference, Int64 amountCents, DateTime expectedTransactionDate, bool open,
            DateTime createdDateTime, int createdByPersonId)
        {
            PayoutId = payoutId;
            OrganizationId = organizationId;
            Bank = bank;
            Account = account;
            Reference = reference;
            AmountCents = amountCents;
            ExpectedTransactionDate = expectedTransactionDate;
            Open = open;
            CreatedDateTime = createdDateTime;
            CreatedByPersonId = createdByPersonId;
        }

        public BasicPayout (BasicPayout original) :
            this (original.PayoutId, original.OrganizationId, original.Bank, original.Account,
                original.Reference, original.AmountCents, original.ExpectedTransactionDate, original.Open,
                original.CreatedDateTime, original.CreatedByPersonId)
        {
            // empty copy ctor
        }

        public int PayoutId { get; private set; }
        public int OrganizationId { get; protected set; }
        public string Bank { get; protected set; }
        public string Account { get; protected set; }
        public string Reference { get; protected set; }
        public Int64 AmountCents { get; protected set; }
        public DateTime ExpectedTransactionDate { get; protected set; }
        public bool Open { get; protected set; }
        public DateTime CreatedDateTime { get; private set; }
        public int CreatedByPersonId { get; private set; }

        #region IHasIdentity Members

        public int Identity
        {
            get { return PayoutId; }
        }

        #endregion
    }
}