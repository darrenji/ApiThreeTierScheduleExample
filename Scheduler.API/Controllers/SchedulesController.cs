using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Scheduler.Data.Abstract;
using Scheduler.Model.Entities;
using Scheduler.API.Core;
using Scheduler.API.ViewModels;
using AutoMapper;
using Scheduler.Model;

// For more information on enabling MVC for empty projects, visit http://go.microsoft.com/fwlink/?LinkID=397860

namespace Scheduler.API.Controllers
{
    [Route("api/[controller]")]
    public class SchedulesController : Controller
    {
        private IScheduleRepository _scheduleRepository;
        private IAttendeeRepository _attendeeRepository;
        private IUserRepository _userRepository;
        int page = 1;
        int pageSize = 4;

        public SchedulesController(IScheduleRepository scheduleRepository,
                                    IAttendeeRepository attendeeRepository,
                                    IUserRepository userRepository)
        {
            _scheduleRepository = scheduleRepository;
            _attendeeRepository = attendeeRepository;
            _userRepository = userRepository;
        }


        /// <summary>
        /// 从前端接受分页数据，把分页信息放在Header中返回给前端，把视图模型返回给前端
        /// </summary>
        /// <returns></returns>
        public IActionResult Get()
        {
            var pagination = Request.Headers["Pagination"];

            if (!string.IsNullOrEmpty(pagination))
            {
                string[] vals = pagination.ToString().Split(',');
                int.TryParse(vals[0], out page);
                int.TryParse(vals[1], out pageSize);
            }

            int currentPage = page;
            int currengPageSize = pageSize;
            var totalSchedulesCount = _scheduleRepository.Count();
            var totalPages = (int)Math.Ceiling((double)totalSchedulesCount / currengPageSize);

            IEnumerable<Schedule> _schedules = _scheduleRepository
                .AllIncluding(t => t.Creator, t => t.Attendees)
                .OrderBy(t => t.Id)
                .Skip((currentPage - 1) * currengPageSize)
                .Take(currengPageSize)
                .ToList();

            Response.AddPagination(page, pageSize, totalSchedulesCount, totalPages);

            IEnumerable<ScheduleViewModel> _schedulesVM = Mapper.Map<IEnumerable<Schedule>, IEnumerable<ScheduleViewModel>>(_schedules);

            return new OkObjectResult(_schedulesVM);
        }

        [HttpGet("{id}", Name ="GetSchedule")]
        public IActionResult Get(int id)
        {
            Schedule _schedule = _scheduleRepository.GetSingle(t => t.Id == id, t => t.Creator, t => t.Attendees);

            if(_schedule!=null)
            {
                ScheduleViewModel _scheduleVM = Mapper.Map<Schedule, ScheduleViewModel>(_schedule);
                return new OkObjectResult(_scheduleVM);
            }
            else
            {
                return NotFound();
            }
        }


        [HttpGet("{id}/details", Name = "GetScheduleDetails")]
        public IActionResult GetScheduleDetails(int id)
        {
            Schedule _schedule = _scheduleRepository.GetSingle(t => t.Id == id, t => t.Creator, t => t.Attendees);

            if(_schedule != null)
            {
                ScheduleDetailsViewModel _scheduleDetailsVM = Mapper.Map<Schedule, ScheduleDetailsViewModel>(_schedule);
                foreach(var attendee in _schedule.Attendees)
                {
                    User _userDb = _userRepository.GetSingle(attendee.UserId);
                    _scheduleDetailsVM.Attendees.Add(Mapper.Map<User, UserViewModel>(_userDb));
                }

                return new OkObjectResult(_scheduleDetailsVM);
            }
            else
            {
                return NotFound();
            }
        }

        [HttpPost]
        public IActionResult Create([FromBody]ScheduleViewModel schedule)
        {
            if(!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            Schedule _newSchedule = Mapper.Map<ScheduleViewModel, Schedule>(schedule);
            _newSchedule.DateCreated = DateTime.Now;

            _scheduleRepository.Add(_newSchedule);
            _scheduleRepository.Commit();

            foreach(var userId in schedule.Attendees)
            {
                _newSchedule.Attendees.Add(new Attendee { UserId = userId });
            }
            _scheduleRepository.Commit();

            schedule = Mapper.Map<Schedule, ScheduleViewModel>(_newSchedule);
            CreatedAtRouteResult result = CreatedAtRoute("GetSchedule", new { controller="Schedules", id=schedule.Id}, schedule);
            return result;
        }


        [HttpPut("{id}")]
        public IActionResult Put(int id, [FromBody]ScheduleViewModel schedule)
        {
            if(!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            //根据主键到数据库查找
            Schedule _scheduleDb = _scheduleRepository.GetSingle(id);
            if (_scheduleDb == null)
            {
                return NotFound();
            }
            else
            {
                _scheduleDb.Title = schedule.Title;
                _scheduleDb.Location = schedule.Location;
                _scheduleDb.Description = schedule.Description;
                _scheduleDb.Status = (ScheduleStatus)Enum.Parse(typeof(ScheduleStatus), schedule.Status);
                _scheduleDb.Type = (ScheduleType)Enum.Parse(typeof(ScheduleType), schedule.Type);
                _scheduleDb.TimeStart = schedule.TimeStart;
                _scheduleDb.TimeEnd = schedule.TimeEnd;

                //处理外联
                _attendeeRepository.DeleteWhere(a => a.ScheduleId == id);
                foreach(var userId in schedule.Attendees)
                {
                    _scheduleDb.Attendees.Add(new Attendee { ScheduleId = id, UserId = userId });
                }
                _scheduleRepository.Commit();
            }
            schedule = Mapper.Map<Schedule, ScheduleViewModel>(_scheduleDb);
            return new NoContentResult();
        }


        [HttpDelete("{id}",Name = "RemoveSchedule")]
        public IActionResult Delete(int id)
        {
            Schedule _scheduleDb = _scheduleRepository.GetSingle(id);

            if(_scheduleDb == null)
            {
                return new NotFoundResult();
            }
            else
            {
                //先处理关联表
                _attendeeRepository.DeleteWhere(a => a.ScheduleId == id);
                _scheduleRepository.Delete(_scheduleDb);
                _scheduleRepository.Commit();
                return new NoContentResult();
            }
        }

        [HttpDelete("{id}/removeattendee/{attendee}")]
        public IActionResult Delete(int id, int attendee)
        {
            Schedule _scheduleDb = _scheduleRepository.GetSingle(id);
            if(_scheduleDb==null)
            {
                return new NotFoundResult();
            }
            else
            {
                _attendeeRepository.DeleteWhere(a => a.ScheduleId == id && a.UserId == attendee);
                _attendeeRepository.Commit();
                return new NoContentResult();
            }
        }
        
    }
}
