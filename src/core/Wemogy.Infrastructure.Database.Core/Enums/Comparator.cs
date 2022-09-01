namespace Wemogy.Infrastructure.Database.Core.Enums;

public enum Comparator
{
    Equals = 0,
    NotEquals = 1,
    Contains = 2,
    ContainsIgnoreCase = 3,
    NotContains = 4,
    StartsWith = 5,
    StartsWithIgnoreCase = 6,
    EndsWith = 7,
    IsOneOf = 8,
    IsNotOneOf = 9,
    GreaterThan = 10,
    GreaterThanEquals = 11,
    LowerThan = 12,
    LowerThanEquals = 13,
    Fuzzy = 14,
    IsEmpty = 15,
    IsNotEmpty = 16,

    /// <summary>
    ///     To be used when a query string contains multiple words separated with the '+' (plus) sign.
    ///     The + symbol should exist in the query at least once.
    ///     Selecting this Comparator forces that all words exist in the results.
    ///     Supports fuzzied results and wildcards (* and ?).
    /// </summary>
    MustContainAllSeparatedWithPlus = 17,

    /// <summary>
    ///     To be used when a query string contains multiple words separated with the '+' (plus) sign.
    ///     The + symbol should exist in the query at least once.
    ///     Selecting this Comparator forces that at least one of the words exists in the results.
    ///     Supports fuzzied results and wildcards (* and ?).
    ///     The more the words found in a result, the higher the score the result gets.
    /// </summary>
    ShouldContainAnySeparatedWithPlus = 18,

    /// <summary>
    ///     To be used when a query string contains wildcards (? for a single character, * for multiple sequential unknown
    ///     characters)
    /// </summary>
    ContainsWildcard = 19,

    /// <summary>
    ///     Combine multiple words, fuzzy search and perfect query matching in a weighted result list
    /// </summary>
    CustomSmartSearch = 20
}
