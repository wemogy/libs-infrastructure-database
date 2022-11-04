using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Attributes;

public class PartitionKeyAttributeTests
{
    private const string Prefix = "a_";

    [Fact]
    public void TestSerializer()
    {
        // arrange
        var user = User.Faker.Generate();
        var tenantId = user.TenantId;

        // act
        var json = JsonSerializer.Serialize(user);

        // assert
        json.Should().Contain($"{Prefix}{tenantId}");
    }

    [Fact]
    public void TestDeserializer()
    {
        // arrange
        var expected = User.Faker.Generate();

        // act
        var json = JsonSerializer.Serialize(expected);

        // assert
        var actual = JsonSerializer.Deserialize<User>(json);
        actual.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void TestCollectionSerializer()
    {
        // arrange
        var users = Enumerable.Range(
                1,
                3)
            .Select(user => User.Faker.Generate());

        // act
        var json = JsonSerializer.Serialize(users);

        // assert
        var numberOfTrues = Regex.Matches(
            json,
            Prefix).Count;
        json.Should().Contain(Prefix);
        numberOfTrues.Should().Be(3);
    }

    [Fact]
    public void TestCollectionDeserializer()
    {
        // arrange
        var expected = Enumerable.Range(
                1,
                3)
            .Select(user => User.Faker.Generate()).ToList();

        // act
        var json = JsonSerializer.Serialize(expected);

        // assert
        var actual = JsonSerializer.Deserialize<List<User>>(json);
        actual.Should().BeEquivalentTo(expected);
    }
}
