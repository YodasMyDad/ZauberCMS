﻿using MediatR;

namespace ZauberCMS.Core.Content.Commands;

public class GetContentLanguagesCommand : IRequest<Dictionary<object, string>>
{
    
}