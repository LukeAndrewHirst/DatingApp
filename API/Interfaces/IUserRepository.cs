using API.DTOs;
using API.Entities;
using API.Helpers;

namespace API.Interfaces
{
    public interface IUserRepository
    {
        void Update(AppUser user);

        Task<IEnumerable<AppUser>> GetUsersAsync();

        Task<AppUser> GetUserByIdAsync(int id);

        Task<AppUser> GetUsernameAsync(string username);

        Task<PagedList<MemberDto>> GetMembersAsync(UserParams userParams);

        Task<MemberDto> GetMemberAysnc(string username);

        Task<string> GetUserGender(string username);

        Task<MemberDto> GetMemberAsync(string username, bool isCurrentUser);

        Task<AppUser> GetUserByPhotoId(int photoId);

        
    }
}