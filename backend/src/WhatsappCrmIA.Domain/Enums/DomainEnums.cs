namespace WhatsappCrmIA.Domain.Enums;

public enum MessageDirection
{
    Inbound = 1,
    Outbound = 2
}

public enum MessageSender
{
    Contact = 1,
    AiAgent = 2,
    HumanAgent = 3,
    System = 4
}

public enum ConversationStatus
{
    Open = 1,
    WaitingHuman = 2,
    Closed = 3
}

public enum ConversationIntent
{
    Unknown = 0,
    GeneralQuestion = 1,
    PriceRequest = 2,
    Scheduling = 3,
    Complaint = 4,
    Other = 5
}

public enum ProposalStatus
{
    Draft = 1,
    SentToClient = 2,
    Accepted = 3,
    Rejected = 4,
    Expired = 5
}

public enum AppointmentStatus
{
    Scheduled = 1,
    Confirmed = 2,
    Cancelled = 3,
    Completed = 4,
    NoShow = 5
}

public enum ReminderChannel
{
    WhatsApp = 1,
    Email = 2
}

public enum ReminderStatus
{
    Pending = 1,
    Sent = 2,
    Failed = 3,
    Cancelled = 4
}

public enum PlanTier
{
    Trial = 0,
    Starter = 1,
    Pro = 2,
    Business = 3
}

public enum UserRole
{
    Owner = 1,
    Agent = 2
}

public enum TemplateScope
{
    Cobranca = 1,
    Lembrete = 2,
    BoasVindas = 3,
    Orcamento = 4,
    Agendamento = 5,
    Outro = 6
}

public enum BulkCampaignStatus
{
    Pending = 1,
    Running = 2,
    Completed = 3,
    Cancelled = 4
}

public enum BulkRecipientStatus
{
    Pending = 1,
    Sent = 2,
    Failed = 3,
    Skipped = 4
}
