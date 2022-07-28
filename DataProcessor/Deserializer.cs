// ReSharper disable InconsistentNaming

namespace TeisterMask.DataProcessor
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Globalization;
    using System.Xml.Serialization;
    using System.Collections.Generic;

    using Newtonsoft.Json;
    using System.ComponentModel.DataAnnotations;

    using ImportDto;
    using Data.Models;
    using Data.Models.Enums;

    using ValidationContext = System.ComponentModel.DataAnnotations.ValidationContext;

    using Data;

    public class Deserializer
    {
        private const string ErrorMessage = "Invalid data!";

        private const string SuccessfullyImportedProject
            = "Successfully imported project - {0} with {1} tasks.";

        private const string SuccessfullyImportedEmployee
            = "Successfully imported employee - {0} with {1} tasks.";

        public static string ImportProjects(TeisterMaskContext context, string xmlString)
        {
            /*Constraints
    • If there are any validation errors for the project entity(such as invalid name or open date),
      do not import any part of the entity and append an error message to the method output.
    • If there are any validation errors for the task entity(such as invalid name, open 
      or due date are missing, task open date is before project open date 
      or task due date is after project due date), do not import it
      (only the task itself, not the whole project) and append an error message to the method output.
NOTE: Dates will be in format dd/ MM / yyyy, do not forget to use CultureInfo.InvariantCulture*/
             StringBuilder sb = new StringBuilder();

            XmlSerializer xmlSerializer = new XmlSerializer(typeof(ImportProjectDto[]), new XmlRootAttribute("Projects"));

            using StringReader stringReader = new StringReader(xmlString);

            ImportProjectDto[] projectDtos = (ImportProjectDto[])xmlSerializer.Deserialize(stringReader);

            List<Project> projects = new List<Project>();

            foreach (ImportProjectDto projectDto in projectDtos)
            {
                if (!IsValid(projectDto))
                {
                    sb.AppendLine(ErrorMessage);
                    continue;
                }

                DateTime openDate;
                bool isOpenDateValid = DateTime.TryParseExact(projectDto.OpenDate, "dd/MM/yyyy",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out openDate);

                if (!isOpenDateValid)
                {
                    sb.AppendLine(ErrorMessage);
                    continue;
                }

                DateTime? dueDate = null;

                if (!String.IsNullOrWhiteSpace(projectDto.DueDate))
                {
                    DateTime dueDateDt;
                    bool isDueDateValid = DateTime.TryParseExact(projectDto.DueDate, "dd/MM/yyyy",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out dueDateDt);

                    if (!isDueDateValid)
                    {
                        sb.AppendLine(ErrorMessage);
                        continue;
                    }

                    dueDate = dueDateDt;
                }

                Project p = new Project()
                {
                    Name = projectDto.Name,
                    OpenDate = openDate,
                    DueDate = dueDate
                };

                foreach (ImportProjectTasksDto taskDto in projectDto.Tasks)
                {
                    if (!IsValid(taskDto))
                    {
                        sb.AppendLine(ErrorMessage);
                        continue;
                    }

                    DateTime taskOpenDate;
                    bool isTaskOpenDateValid = DateTime.TryParseExact(taskDto.OpenDate, "dd/MM/yyyy",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out taskOpenDate);

                    if (!isTaskOpenDateValid)
                    {
                        sb.AppendLine(ErrorMessage);
                        continue;
                    }

                    DateTime taskDueDate;
                    bool isTaskDueDateValid = DateTime.TryParseExact(taskDto.DueDate, "dd/MM/yyyy",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out taskDueDate);

                    if (!isTaskDueDateValid)
                    {
                        sb.AppendLine(ErrorMessage);
                        continue;
                    }

                    if (taskOpenDate < openDate)
                    {
                        sb.AppendLine(ErrorMessage);
                        continue;
                    }

                    if (dueDate.HasValue && taskDueDate > dueDate.Value)
                    {
                        sb.AppendLine(ErrorMessage);
                        continue;
                    }

                    Task t = new Task()
                    {
                        Name = taskDto.Name,
                        OpenDate = taskOpenDate,
                        DueDate = taskDueDate,
                        ExecutionType = (ExecutionType)taskDto.ExecutionType,
                        LabelType = (LabelType)taskDto.LabelType
                    };

                    p.Tasks.Add(t);
                }

                projects.Add(p);
                sb.AppendLine(String.Format(SuccessfullyImportedProject, p.Name, p.Tasks.Count));
            }

            context.Projects.AddRange(projects);
            context.SaveChanges();

            return sb.ToString().TrimEnd();
        }

        public static string ImportEmployees(TeisterMaskContext context, string jsonString)
        {
    /*Constraints
    • If any validation errors occur(such as invalid username, email or phone),
      do not import any part of the entity and append an error message to the method output.
    • Take only the unique tasks.
    • If a task does not exist in the database,
      append an error message to the method output and continue with the next task.*/
            StringBuilder sb = new StringBuilder();

            ImportEmployeeDto[] employeeDtos = JsonConvert.DeserializeObject<ImportEmployeeDto[]>(jsonString);

            List<Employee> employees = new List<Employee>();

            foreach (ImportEmployeeDto employeeDto in employeeDtos)
            {
                if (!IsValid(employeeDto))
                {
                    sb.AppendLine(ErrorMessage);
                    continue;
                }

                Employee e = new Employee()
                {
                    Username = employeeDto.Username,
                    Email = employeeDto.Email,
                    Phone = employeeDto.Phone
                };

                foreach (int taskId in employeeDto.Tasks.Distinct())
                {
                    Task t = context.Tasks.Find(taskId);

                    if (t == null)
                    {
                        sb.AppendLine(ErrorMessage);
                        continue;
                    }

                    e.EmployeesTasks.Add(new EmployeeTask()
                    {
                        Task = t
                    });
                }

                employees.Add(e);
                sb.AppendLine(String.Format(SuccessfullyImportedEmployee, e.Username, e.EmployeesTasks.Count));
            }

            context.Employees.AddRange(employees);
            context.SaveChanges();

            return sb.ToString().TrimEnd();
        }


        private static bool IsValid(object dto)
        {
            var validationContext = new ValidationContext(dto);
            var validationResult = new List<ValidationResult>();

            return Validator.TryValidateObject(dto, validationContext, validationResult, true);
        }
    }
}