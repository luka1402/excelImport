using ExcelImporter.Domain.Rules;
using FluentValidation;

namespace ExcelImporter.Application.Rules;

/// <summary>Validira bilo koje pravilo; konkretna provera se bira po tipu pravila.</summary>
public sealed class RuleValidator : AbstractValidator<Rule>
{
    public RuleValidator()
    {
        RuleFor(rule => rule.Version)
            .Must((rule, version) => RuleTypes.IsSupportedVersion(rule.Type, version))
            .WithMessage(rule => $"Verzija {rule.Version} pravila tipa \"{rule.Type}\" nije podržana.");

        RuleFor(rule => rule).SetInheritanceValidator(validators =>
        {
            validators.Add(new ConditionalValueRuleValidator());
            validators.Add(new DefaultValueRuleValidator());
            validators.Add(new TransformRuleValidator());
            validators.Add(new LookupRuleValidator());
            validators.Add(new RowFilterRuleValidator());
        });
    }
}

public sealed class ConditionValidator : AbstractValidator<Condition>
{
    public ConditionValidator()
    {
        RuleFor(condition => condition.Field).NotEmpty().WithMessage("Uslov mora imati kolonu.");
        RuleFor(condition => condition.Operator).IsInEnum().WithMessage("Nepoznat operator.");
        RuleFor(condition => condition.Value)
            .NotEmpty().When(condition => condition.RequiresValue)
            .WithMessage("Operator zahteva vrednost za poređenje.");
        RuleFor(condition => condition.Value)
            .Null().When(condition => !condition.RequiresValue)
            .WithMessage("Operatori isEmpty i isNotEmpty ne koriste vrednost.");
    }
}

public sealed class ConditionalValueRuleValidator : AbstractValidator<ConditionalValueRule>
{
    public ConditionalValueRuleValidator()
    {
        RuleFor(rule => rule.TargetField).NotEmpty();
        RuleFor(rule => rule.Conditions).NotEmpty().WithMessage("Pravilo mora imati bar jedan uslov.");
        RuleForEach(rule => rule.Conditions).NotNull().SetValidator(new ConditionValidator());
        RuleFor(rule => rule.Value).NotNull().WithMessage("Vrednost koja se postavlja je obavezna.");
    }
}

public sealed class DefaultValueRuleValidator : AbstractValidator<DefaultValueRule>
{
    public DefaultValueRuleValidator()
    {
        RuleFor(rule => rule.TargetField).NotEmpty();
        RuleFor(rule => rule.Value).NotEmpty().WithMessage("Podrazumevana vrednost je obavezna.");
    }
}

public sealed class TransformRuleValidator : AbstractValidator<TransformRule>
{
    public TransformRuleValidator()
    {
        RuleFor(rule => rule.TargetField).NotEmpty();
        RuleFor(rule => rule.Kind).IsInEnum().WithMessage("Nepoznata transformacija.");
        RuleFor(rule => rule.Format).NotEmpty().When(rule => rule.Kind == TransformKind.DateFormat)
            .WithMessage("Transformacija dateFormat zahteva format datuma.");
        RuleFor(rule => rule.Separator).Must(separator => separator is "," or ".")
            .When(rule => rule.Kind == TransformKind.DecimalSeparator)
            .WithMessage("Decimalni separator mora biti \",\" ili \".\".");
    }
}

public sealed class LookupRuleValidator : AbstractValidator<LookupRule>
{
    public LookupRuleValidator()
    {
        RuleFor(rule => rule.TargetField).NotEmpty();
        RuleFor(rule => rule.SourceField).NotEmpty();
        RuleFor(rule => rule.Mappings).NotEmpty().WithMessage("Tabela zamena ne sme biti prazna.");
        RuleFor(rule => rule.Mappings)
            .Must(mappings => mappings.Keys.All(key => !string.IsNullOrWhiteSpace(key)))
            .WithMessage("Ključevi tabele zamena ne smeju biti prazni.");
    }
}

public sealed class RowFilterRuleValidator : AbstractValidator<RowFilterRule>
{
    public RowFilterRuleValidator()
    {
        RuleFor(rule => rule.Action).IsInEnum().WithMessage("Nepoznata akcija filtera.");
        RuleFor(rule => rule.Conditions).NotEmpty().WithMessage("Filter mora imati bar jedan uslov.");
        RuleForEach(rule => rule.Conditions).NotNull().SetValidator(new ConditionValidator());
    }
}