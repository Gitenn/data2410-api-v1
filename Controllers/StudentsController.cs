using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using data2410_api_v1.Models;

namespace data2410_api_v1.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StudentsController(IConfiguration config) : ControllerBase
{
    private readonly string _connectionString = config.GetConnectionString("DefaultConnection")!;

    private static string GetGrade(int marks) => marks switch
    {
        >= 90 => "A",
        >= 80 => "B",
        >= 60 => "C",
        _ => "D"
    };

    [HttpGet]
    public async Task<ActionResult<List<Student>>> GetAll()
    {
        var students = new List<Student>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = new SqlCommand("SELECT Id, Name, Course, Marks, Grade FROM Students", conn);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            students.Add(new Student
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Course = reader.GetString(2),
                Marks = reader.GetInt32(3),
                Grade = reader.IsDBNull(4) ? null : reader.GetString(4)
            });
        }

        return students;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Student>> GetById(int id)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = new SqlCommand("SELECT Id, Name, Course, Marks, Grade FROM Students WHERE Id = @Id", conn);
        cmd.Parameters.AddWithValue("@Id", id);

        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return NotFound();

        return new Student
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            Course = reader.GetString(2),
            Marks = reader.GetInt32(3),
            Grade = reader.IsDBNull(4) ? null : reader.GetString(4)
        };
    }

    [HttpPost]
    public async Task<ActionResult<Student>> Create(Student student)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = new SqlCommand(
            "INSERT INTO Students (Name, Course, Marks) OUTPUT INSERTED.Id VALUES (@Name, @Course, @Marks)", conn);
        cmd.Parameters.AddWithValue("@Name", student.Name);
        cmd.Parameters.AddWithValue("@Course", student.Course);
        cmd.Parameters.AddWithValue("@Marks", student.Marks);

        var newId = await cmd.ExecuteScalarAsync();
        if (newId is null || newId == DBNull.Value)
        {
            return BadRequest("Could not create student.");
        }

        student.Id = Convert.ToInt32(newId);
        return CreatedAtAction(nameof(GetById), new { id = student.Id }, student);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, Student updated)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = new SqlCommand(
            "UPDATE Students SET Name = @Name, Course = @Course, Marks = @Marks WHERE Id = @Id", conn);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Name", updated.Name);
        cmd.Parameters.AddWithValue("@Course", updated.Course);
        cmd.Parameters.AddWithValue("@Marks", updated.Marks);

        var rows = await cmd.ExecuteNonQueryAsync();
        return rows == 0 ? NotFound() : NoContent();
    }

    public async Task<bool> UpdateGrade(int id, Student updated)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = new SqlCommand(
            "UPDATE Students SET Grade = @Grade WHERE Id = @Id", conn);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Grade", updated.Grade);

        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    [HttpPost("calculate-grades")]
    public async Task<ActionResult<List<Student>>> CalculateGrades()
    {
        var studentsWithGrade = new List<Student>();
        var students = await GetAll();
        if (students.Value == null) return NotFound();

        foreach (var student in students.Value)
        {
            student.Grade = GetGrade(student.Marks);
            var updateSuccess = await UpdateGrade(student.Id, student);

            if(!updateSuccess){
                Console.WriteLine($"Failed to update grade for student with ID {student.Id}");
            } else{
                studentsWithGrade.Add(student);
            }
        }

        return studentsWithGrade;   
    }


    
    [HttpGet("report")]
public async Task<IActionResult> Report()
{
    var reports = new List<CourseReport>();
    using var conn = new SqlConnection(_connectionString);
    await conn.OpenAsync();

    using var cmd = new SqlCommand(@"
        SELECT Course, 
            COUNT(*) as TotalStudents, 
            AVG(CAST(Marks AS FLOAT)) as AverageMarks, 
            SUM(CASE WHEN Marks >= 90 THEN 1 ELSE 0 END) as A,
            SUM(CASE WHEN Marks >= 80 AND Marks < 90 THEN 1 ELSE 0 END) as B,
            SUM(CASE WHEN Marks >= 60 AND Marks < 80 THEN 1 ELSE 0 END) as C,
            SUM(CASE WHEN Marks < 60 THEN 1 ELSE 0 END) as D
        FROM Students 
        GROUP BY Course", conn);

    using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        reports.Add(new CourseReport
        {
            CourseName = reader.GetString(0),
            TotalStudents = reader.GetInt32(1),
            AverageMarks = reader.GetDouble(2),
            GradeDistribution = new GradeDistribution
            {
                A = reader.GetInt32(3),
                B = reader.GetInt32(4),
                C = reader.GetInt32(5),
                D = reader.GetInt32(6)
            }
        });
    }

    return Ok(reports);
}

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = new SqlCommand("DELETE FROM Students WHERE Id = @Id", conn);
        cmd.Parameters.AddWithValue("@Id", id);

        var rows = await cmd.ExecuteNonQueryAsync();
        return rows == 0 ? NotFound() : NoContent();
    }
}
