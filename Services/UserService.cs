using Leafy_Library.Models;
using MongoDB.Driver;

namespace Leafy_Library.Services;

public class UserService
{
    private readonly IMongoCollection<User> _usersCollection;

    public UserService(DatabaseService db)
    {
        _usersCollection = db.Users;
    }

    public async Task<User> GetOrCreateUserAsync(string username)
    {
        var user = await _usersCollection
            .Find(u => u.Username == username)
            .FirstOrDefaultAsync();

        if (user is null)
        {
            user = new User
            {
                Username = username,
                IsAdmin = false
            };
            await _usersCollection.InsertOneAsync(user);
        }

        return user;
    }

    public async Task<User?> GetUserByIdAsync(string id)
    {
        return await _usersCollection
            .Find(u => u.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        return await _usersCollection
            .Find(u => u.Username == username)
            .FirstOrDefaultAsync();
    }
}
