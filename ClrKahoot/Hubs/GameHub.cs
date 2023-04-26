using Microsoft.AspNetCore.SignalR;

namespace ClrKahoot.Hubs
{
    public class GameHub : Hub
    {
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // get the user
            if (ConnectionDetails.TryGetGroupByUser(Context.ConnectionId, out GroupDetails groupdetails, out UserDetails userdetails))
            {
                // check if owner
                var isowner = groupdetails.IsOwner(userdetails);

                // remove this user
                groupdetails.RemoveUser(userdetails);

                // if the owner was removed, the game is over
                if (isowner)
                {
                    // notify everyone that the game is over
                    await Clients.Group(groupdetails.Name).SendAsync("ReceiveMessage", "the owner of this game has disconnected and the game is over");
                }

                // update the participant list
                await SendParticipants(groupdetails.Name);
            }
        }

        public async Task SendJoinGame(string group, string user)
        {
            // get the UserDetails
            var groupdetails = ConnectionDetails.GetOrCreateGroup(group);
            var userdetails = groupdetails.GetOrCreateUser(user, Context.ConnectionId);

            // associate connection with group
            await Groups.AddToGroupAsync(Context.ConnectionId, group);

            // check if owner
            if (groupdetails.IsOwner(userdetails))
            {
                await Clients.Caller.SendAsync("ReceiveIsOwner");
            }

            // send the full participants list
            await SendParticipants(group);
        }

        public async Task SendAddUpdateQuestion(string group, int number, string content, string ansCorrect, string ans1, string ans2, string ans3)
        {
            // the owner 

            // get the group
            if (ConnectionDetails.TryGetGroup(group, out GroupDetails groupdetails))
            {
                // get the user
                if (groupdetails.TryGetUser(Context.ConnectionId, out UserDetails userdetails))
                {
                    // verify that this user is the owner
                    if (groupdetails.IsOwner(userdetails))
                    {
                        // add or update question 'number'
                        if (!groupdetails.TryAddUpdateQuestion(number, content, ansCorrect, ans1, ans2, ans3))
                        {
                            await Clients.Caller.SendAsync("ReceiveMessage", "unable to add/update question");
                        }
                    }
                }
                else
                {
                    await Clients.Caller.SendAsync("ReceiveMessage", "unable to find user (please reload)");
                }
            }
            else
            {
                await Clients.Caller.SendAsync("ReceiveMessage", "unable to find group (please reload)");
            }
        }

        public async Task SendDeleteQuestion(string group, int number)
        {
            // the owner 

            // get the group
            if (ConnectionDetails.TryGetGroup(group, out GroupDetails groupdetails))
            {
                // get the user
                if (groupdetails.TryGetUser(Context.ConnectionId, out UserDetails userdetails))
                {
                    // verify that this user is the owner
                    if (groupdetails.IsOwner(userdetails))
                    {
                        // add or update question 'number'
                        if (!groupdetails.TryDeleteQuestion(number))
                        {
                            await Clients.Caller.SendAsync("ReceiveMessage", "unable to delete question");
                        }
                    }
                }
                else
                {
                    await Clients.Caller.SendAsync("ReceiveMessage", "unable to find user (please reload)");
                }
            }
            else
            {
                await Clients.Caller.SendAsync("ReceiveMessage", "unable to find group (please reload)");
            }
        }

        public async Task SendGetQuestion(string group, int number, bool broadcast)
        {
            // get the group
            if (ConnectionDetails.TryGetGroup(group, out GroupDetails groupdetails))
            {
                // get the user
                if (groupdetails.TryGetUser(Context.ConnectionId, out UserDetails userdetails))
                {
                    // verify that this user is the owner
                    if (groupdetails.IsOwner(userdetails))
                    {
                        // get this question
                        if (groupdetails.TryGetQuestion(number, out Question question))
                        {
                            // should be impossible - inconsistent data
                            if (question.Answers.Length != 3)
                            {
                                await Clients.Caller.SendAsync("ReceiveMessage", $"invalid question #{number}");
                                return;
                            }

                            if (!broadcast)
                            {
                                // send to just the owner
                                await Clients.Caller.SendAsync("ReceiveQuestion", number, groupdetails.NumberQuestions, broadcast, question.Content, question.CorrectAnswer, question.Answers[0], question.Answers[1], question.Answers[2]);
                            }
                            else
                            {
                                // send to everyone in the group and shuffle the answers

                                // do not block sending active questions, as this is the fastest way to add a player mid question

                                // mark the question as active
                                groupdetails.SetQuestionActive(number, active: true);

                                // shuffle the answers
                                var ans = new string[]
                                {
                                    question.CorrectAnswer,
                                    question.Answers[0],
                                    question.Answers[1],
                                    question.Answers[2]
                                };
                                var rand = new Random();
                                for (int c = 0; c < ans.Length; c++)
                                {
                                    var i = c;
                                    // get a random index
                                    var j = i;
                                    while (j == i) j = rand.Next() % ans.Length;

                                    // swap
                                    var tmp = ans[i];
                                    ans[i] = ans[j];
                                    ans[j] = tmp;
                                }

                                // send to everyone in the group
                                await Clients.Group(group).SendAsync("ReceiveQuestion", number, groupdetails.NumberQuestions, broadcast, question.Content, ans[0], ans[1], ans[2], ans[3]);
                            }
                        }
                        else
                        {
                            if (!broadcast)
                            {
                                // send an empty question
                                await Clients.Caller.SendAsync("ReceiveQuestion", number, groupdetails.NumberQuestions, broadcast, "", "", "", "", "");
                            }
                            else
                            {
                                await Clients.Caller.SendAsync("ReceiveMessage", "unable to broadcast question as it is not found");
                            }
                        }
                    }
                }
                else
                {
                    await Clients.Caller.SendAsync("ReceiveMessage", "unable to find user (please reload)");
                }
            }
            else
            {
                await Clients.Caller.SendAsync("ReceiveMessage", "unable to find group (please reload)");
            }
        }

        public async Task SendFinishQuestion(string group, int number)
        {
            // get the group
            if (ConnectionDetails.TryGetGroup(group, out GroupDetails groupdetails))
            {
                // get the user
                if (groupdetails.TryGetUser(Context.ConnectionId, out UserDetails userdetails))
                {
                    // verify that this user is the owner
                    if (groupdetails.IsOwner(userdetails))
                    {
                        // get this question
                        if (groupdetails.TryGetQuestion(number, out Question question))
                        {
                            // mark the question as in-active
                            groupdetails.SetQuestionActive(number, active: false);

                            // send everyone the answer
                            await Clients.Group(group).SendAsync("ReceiveAnswer", number, question.CorrectAnswer);

                            // send out the participant list and scores
                            await SendParticipants(group);
                        }
                    }
                }
                else
                {
                    await Clients.Caller.SendAsync("ReceiveMessage", "unable to find user (please reload)");
                }
            }
            else
            {
                await Clients.Caller.SendAsync("ReceiveMessage", "unable to find group (please reload)");
            }
        }

        public async Task SendAnswer(string group, int number, string ans)
        {
            // get the group
            if (ConnectionDetails.TryGetGroup(group, out GroupDetails groupdetails))
            {
                // get the user
                if (groupdetails.TryGetUser(Context.ConnectionId, out UserDetails userdetails))
                {
                    // get the correct answer
                    if (!groupdetails.TryGetQuestion(number, out Question question))
                    {
                        await Clients.Caller.SendAsync("ReceiveMessage", "failed to answer question");
                    }

                    // check if the question is still active
                    if (!groupdetails.IsQuestionActive(number)) return;

                    // check if the answer is correct
                    var correct = string.Equals(question.CorrectAnswer, ans, StringComparison.OrdinalIgnoreCase);

                    // set the users points (only accept 1 answer)
                    if (userdetails.TrySetPoints(number, correct))
                    {
                        // indicate back to the user that this question is done
                        await Clients.Caller.SendAsync("ReceiveQuestionComplete", number, ans);
                    }

                    // send out stats on the count of who answered
                    if (groupdetails.TryGetQuestionStats(number, out int userCount, out int userCompleteCount))
                    {
                        await Clients.Group(group).SendAsync("ReceiveQuestionStats", userCount, userCompleteCount);
                    }
                }
                else
                {
                    await Clients.Caller.SendAsync("ReceiveMessage", "unable to find user (please reload)");
                }
            }
            else
            {
                await Clients.Caller.SendAsync("ReceiveMessage", "unable to find group (please reload)");
            }
        }

        public async Task SendParticipants(string group)
        {
            // get the group
            if (ConnectionDetails.TryGetGroup(group, out GroupDetails groupdetails))
            {
                var users = groupdetails.GetUsers();
                if (users != null && users.Count > 0)
                {
                    // send the updated list of users
                    var allusers = users.Select(u => $"{u.ConnectionId},{u.UserName},{u.SumPoints}").ToList();
                    var json = System.Text.Json.JsonSerializer.Serialize(allusers);
                    await Clients.Group(groupdetails.Name).SendAsync("ReceiveParticipants", json);
                }
            }
        }

        #region private
        class UserDetails
        {
            public string ConnectionId { get; set; }
            public string UserName { get; set; }
            public Dictionary<int, bool> Points { get; private set; }

            public int SumPoints
            {
                get
                {
                    lock(Points)
                    {
                        var sum = 0;
                        foreach (var kvp in Points) if (kvp.Value) sum++;
                        return sum;
                    }
                }
            }

            public bool TrySetPoints(int number, bool correct)
            {
                lock (Points)
                {
                    // check if the user has already answered this question
                    if (!Points.ContainsKey(number))
                    {
                        // apply the score to the user
                        Points.Add(number, correct);
                        return true;
                    }
                }
                return false;
            }

            public bool HasSetPoints(int number)
            {
                // check if the user has already answered this question
                lock (Points)
                {
                    return Points.ContainsKey(number);
                }
            }

            public UserDetails()
            {
                Points = new Dictionary<int, bool>();
            }
        }

        class Question
        {
            public string Content { get; set; }
            public string[] Answers { get; set; }
            public string CorrectAnswer { get; set; }

            #region internal
            internal bool Active;
            #endregion
        }

        class GroupDetails
        {
            public GroupDetails(string groupname)
            {
                Name = groupname;
                Users = new Dictionary<string, UserDetails>();
                GroupLock = new ReaderWriterLockSlim();
                Questions = new List<Question>();
            }

            public string Name { get; private set; }

            public UserDetails GetOrCreateUser(string username, string connectionid)
            {
                UserDetails userdetails = null;
                try
                {
                    GroupLock.EnterUpgradeableReadLock();
                    if (Users.TryGetValue(connectionid, out userdetails)) return userdetails;

                    // try to add them 
                    try
                    {
                        GroupLock.EnterWriteLock();
                        if (Users.TryGetValue(connectionid, out userdetails)) return userdetails;

                        // add them
                        userdetails = new UserDetails() { ConnectionId = connectionid, UserName = username };
                        Users.Add(connectionid, userdetails);
                    }
                    finally
                    {
                        GroupLock.ExitWriteLock();
                    }
                }
                finally
                {
                    GroupLock.ExitUpgradeableReadLock();
                }

                // in the event that this is a new user, try to elevat them to owner
                TryElevateUserToOwner(userdetails);
                return userdetails;
            }

            public bool TryGetUser(string connectionid, out UserDetails userdetails)
            {
                try
                {
                    GroupLock.EnterReadLock();
                    if (Users.TryGetValue(connectionid, out userdetails)) return true;
                    return false;
                }
                finally
                {
                    GroupLock.ExitReadLock();
                }
            }

            public bool RemoveUser(UserDetails user)
            {
                try
                {
                    GroupLock.EnterWriteLock();
                    if (IsOwner(user)) Owner = null;
                    return Users.Remove(user.ConnectionId);
                }
                finally
                {
                    GroupLock.ExitWriteLock();
                }
            }

            public bool IsOwner(UserDetails user)
            {
                return user != null && Owner != null && string.Equals(user.ConnectionId, Owner.ConnectionId, StringComparison.OrdinalIgnoreCase);
            }

            public bool TryElevateUserToOwner(UserDetails userdetails)
            {
                try
                {
                    GroupLock.EnterUpgradeableReadLock();
                    if (Owner != null || userdetails == null) return false;
                    try
                    {
                        GroupLock.EnterWriteLock();
                        if (Owner != null || userdetails == null) return false;

                        // elevate
                        Owner = userdetails;
                        return true;
                    }
                    finally
                    {
                        GroupLock.ExitWriteLock();
                    }
                }
                finally
                {
                    GroupLock.ExitUpgradeableReadLock();
                }
            }

            public List<UserDetails> GetUsers()
            {
                try
                {
                    GroupLock.EnterReadLock();
                    return Users.Values.ToList();
                }
                finally
                {
                    GroupLock.ExitReadLock();
                }
            }

            public bool TryAddUpdateQuestion(int number, string content, string correctAns, string ans1, string ans2, string ans3)
            {
                try
                {
                    GroupLock.EnterWriteLock();
                    // out of bounds
                    if (number < 0 || number > Questions.Count) return false;

                    // new question
                    if (number == Questions.Count)
                    {
                        // simple validation
                        if (string.IsNullOrWhiteSpace(content)) return false;

                        // add the question
                        Questions.Add(
                            new Question()
                            {
                                Content = Sanitize(content),
                                CorrectAnswer = Sanitize(correctAns),
                                Answers = new string[] { Sanitize(ans1), Sanitize(ans2), Sanitize(ans3) },
                                Active = false
                            }
                            );
                        return true;
                    }

                    // update question
                    Questions[number].Content = Sanitize(content);
                    Questions[number].CorrectAnswer = Sanitize(correctAns);
                    Questions[number].Answers = new string[] { Sanitize(ans1), Sanitize(ans2), Sanitize(ans3) };
                    Questions[number].Active = false;
                    return true;
                }
                finally
                {
                    GroupLock.ExitWriteLock();
                }
            }

            public bool IsQuestionActive(int number)
            {
                try
                {
                    GroupLock.EnterReadLock();
                    // out of bounds
                    if (number < 0 || number >= Questions.Count) return false;
                    return Questions[number].Active;
                }
                finally
                {
                    GroupLock.ExitReadLock();
                }
            }

            public void SetQuestionActive(int number, bool active)
            {
                try
                {
                    GroupLock.EnterReadLock();
                    // out of bounds
                    if (number < 0 || number >= Questions.Count) return;
                    Questions[number].Active = active;
                }
                finally
                {
                    GroupLock.ExitReadLock();
                }
            }

            public bool TryGetQuestion(int number, out Question question)
            {
                try
                {
                    GroupLock.EnterReadLock();
                    if (number >= 0 && number < Questions.Count)
                    {
                        // add the question
                        question = Questions[number];
                        return true;
                    }
                    // does not exist
                    question = null;
                    return false;
                }
                finally
                {
                    GroupLock.ExitReadLock();
                }
            }

            public bool TryDeleteQuestion(int number)
            {
                try
                {
                    GroupLock.EnterReadLock();
                    if (number >= 0 && number < Questions.Count)
                    {
                        // add the question
                        Questions.RemoveAt(number);
                        return true;
                    }
                    return false;
                }
                finally
                {
                    GroupLock.ExitReadLock();
                }
            }

            public bool TryGetQuestionStats(int number, out int userCount, out int userCompleteCount)
            {
                try
                {
                    GroupLock.EnterReadLock();
                    userCount = userCompleteCount = 0;

                    // if valid question, return stats
                    if (number >= 0 && number < Questions.Count)
                    {
                        // iterate through users and gather counts
                        foreach(var user in Users.Values)
                        {
                            // increment
                            userCount++;
                            if (user.HasSetPoints(number)) userCompleteCount++;
                        }

                        return true;
                    }

                    return false;
                }
                finally
                {
                    GroupLock.ExitReadLock();
                }
            }

            public int NumberQuestions
            {
                get
                {
                    try
                    {
                        GroupLock.EnterReadLock();
                        return Questions.Count;
                    }
                    finally
                    {
                        GroupLock.ExitReadLock();
                    }
                }
            }

            #region private
            private Dictionary<string /*connectionid*/, UserDetails> Users;
            private ReaderWriterLockSlim GroupLock;
            private UserDetails Owner;
            private List<Question> Questions;

            private static string Sanitize(string code)
            {
                // replace '\n' with '&#10;'
                // replace '<' with '&lt;'
                return code.Replace("\\n", "&#10;").Replace("<", "&lt;");
            }
            #endregion
        }

        static class ConnectionDetails
        {
            public static GroupDetails GetOrCreateGroup(string group)
            {
                try
                {
                    ConnectionsLock.EnterUpgradeableReadLock();
                    GroupDetails groupdetails = null;
                    if (!Connections.TryGetValue(group, out groupdetails))
                    {
                        try
                        {
                            ConnectionsLock.EnterWriteLock();
                            if (!Connections.TryGetValue(group, out groupdetails))
                            {
                                groupdetails = new GroupDetails(groupname: group);
                                Connections.Add(group, groupdetails);
                            }
                        }
                        finally
                        {
                            ConnectionsLock.ExitWriteLock();
                        }
                    }
                    return groupdetails;
                }
                finally
                {
                    ConnectionsLock.ExitUpgradeableReadLock();
                }
            }

            public static bool TryGetGroup(string group, out GroupDetails groupdetails)
            {
                try
                {
                    ConnectionsLock.EnterReadLock();
                    if (Connections.TryGetValue(group, out groupdetails)) return true;
                    return false;
                }
                finally
                {
                    ConnectionsLock.ExitReadLock();
                }
            }

            public static bool TryGetGroupByUser(string connectionid, out GroupDetails groupdetails, out UserDetails userdetails)
            {
                try
                {
                    ConnectionsLock.EnterReadLock();
                    foreach (var connection in Connections.Values)
                    {
                        if (connection.TryGetUser(connectionid, out userdetails))
                        {
                            groupdetails = connection;
                            return true;
                        }
                    }
                    groupdetails = null;
                    userdetails = null;
                    return false;
                }
                finally
                {
                    ConnectionsLock.ExitReadLock();
                }
            }

            #region private
            private static ReaderWriterLockSlim ConnectionsLock = new ReaderWriterLockSlim();
            private static Dictionary<string /*group*/, GroupDetails> Connections = new Dictionary<string, GroupDetails>();
            #endregion
        }
        #endregion
    }
}
