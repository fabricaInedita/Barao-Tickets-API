﻿using BaraoFeedback.Domain.Entities.Base;

namespace BaraoFeedback.Domain.Entities;

public class TicketCategory : Entity
{
    public string Description { get; set; }
    public bool IsActive { get; set; }
    public List<Ticket>? Tickets { get; set; }

    public void Update(string description)
    {
        if (!string.IsNullOrEmpty(description)) 
            Description = description; 
    }

}