namespace TeisterMask.DataProcessor
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Globalization;
    using System.Xml.Serialization;

    using AutoMapper;

    using Newtonsoft.Json;

    using Data;
    using Data.Models;
    using ExportDto;

    using Formatting = Newtonsoft.Json.Formatting;

    public class Serializer
    {
        public static string ExportProjectWithTheirTasks(TeisterMaskContext context)
        {
/*Export all projects that have at least one task.For each project, export its name, tasks count,
 * and if it has end(due) date which is represented like "Yes" and "No".For each task,
 * export its name and label type.Order the tasks by name(ascending).
 * Order the projects by tasks count(descending), then by name(ascending).
NOTE: You may need to call.ToArray() function before the selection in order to
detach entities from the database and avoid runtime errors(EF Core bug).*/
            StringBuilder sb = new StringBuilder();

            XmlSerializer xmlSerializer = new XmlSerializer(typeof(ExportProjectDto[]), new XmlRootAttribute("Projects"));

            XmlSerializerNamespaces namespaces = new XmlSerializerNamespaces();
            namespaces.Add(string.Empty, string.Empty);

            using StringWriter sw = new StringWriter(sb);

            Project[] projects = context
                .Projects
                .Where(p => p.Tasks.Any())
                .ToArray();
            //.Select(p => new ExportProjectDto()
            //{
            //    Name = p.Name,
            //    HasEndDate = p.DueDate.HasValue ? "Yes" : "No",
            //    TasksCount = p.Tasks.Count,
            //    Tasks = p.Tasks
            //        .ToArray()
            //        .Select(t => new ExportProjectTaskDto()
            //        {
            //            Name = t.Name,
            //            Label = t.LabelType.ToString()
            //        })
            //        .OrderBy(t => t.Name)
            //        .ToArray()
            //})
            //.OrderByDescending(p => p.TasksCount)
            //.ThenBy(p => p.Name)
            //.ToArray();

            ExportProjectDto[] projectDtos = Mapper.Map<ExportProjectDto[]>(projects)
                .OrderByDescending(p => p.TasksCount)
                .ThenBy(p => p.Name)
                .ToArray();

            xmlSerializer.Serialize(sw, projectDtos, namespaces);

            return sb.ToString().TrimEnd();
        }

        public static string ExportMostBusiestEmployees(TeisterMaskContext context, DateTime date)
        {
/*Select the top 10 employees who have at least one task that its open date is after or equal to the given date
 *with their tasks that meet the same requirement(to have their open date after or equal to the giver date).
 *For each employee, export their username and their tasks.For each task, export its name and open date
 *(must be in format "d"), due date(must be in format "d"), 
 *label and execution type. Order the tasks by due date(descending), 
 *then by name(ascending).Order the employees by all tasks(meeting above condition) count(descending),
 *then by username(ascending).
NOTE: Do not forget to use CultureInfo.InvariantCulture.You may need to call.ToArray() function 
before the selection in order to detach entities from the database and avoid runtime errors(EF Core bug).*/
            var employees = context
                .Employees
                .Where(e => e.EmployeesTasks.Any(et => et.Task.OpenDate >= date))
                .ToArray()
                .Select(e => new
                {
                    e.Username,
                    Tasks = e.EmployeesTasks
                        .Where(et => et.Task.OpenDate >= date)
                        .ToArray()
                        .OrderByDescending(et => et.Task.DueDate)
                        .ThenBy(et => et.Task.Name)
                        .Select(et => new
                        {
                            TaskName = et.Task.Name,
                            OpenDate = et.Task.OpenDate.ToString("d", CultureInfo.InvariantCulture),
                            DueDate = et.Task.DueDate.ToString("d", CultureInfo.InvariantCulture),
                            LabelType = et.Task.LabelType.ToString(),
                            ExecutionType = et.Task.ExecutionType.ToString()
                        })
                        .ToArray()
                })
                .OrderByDescending(e => e.Tasks.Length)
                .ThenBy(e => e.Username)
                .Take(10)
                .ToArray();

            return JsonConvert.SerializeObject(employees, Formatting.Indented);
        }
    }
}