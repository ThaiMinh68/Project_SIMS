using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using SIMS.Application.DTOs;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Interfaces;
using Xunit;

namespace SIMS.Tests;

public class CourseServiceTests
{
    [Fact]
    public async Task GetCourseByIdAsync_CourseExists_ReturnsCourse()
    {
        // Arrange
        var courseId = Guid.NewGuid();
        var course = new Course { Id = courseId, Code = "C101", Title = "Intro" };

        var courseRepo = new Mock<IGenericRepository<Course>>();
        courseRepo.Setup(r => r.GetByIdAsync(courseId)).ReturnsAsync(course);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.Repository<Course>()).Returns(courseRepo.Object);

        var svc = new CourseService(uow.Object);

        // Act
        var result = await svc.GetCourseByIdAsync(courseId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeSameAs(course);
    }

    [Fact]
    public async Task CreateCourseAsync_ValidDto_ReturnsTrue()
    {
        // Arrange
        var dto = new CreateCourseDto
        {
            Code = "C102",
            Title = "Algorithms",
            Credits = 3,
            DepartmentId = Guid.NewGuid()
        };

        var courseRepo = new Mock<IGenericRepository<Course>>();
        courseRepo.Setup(r => r.AddAsync(It.IsAny<Course>())).Returns(Task.CompletedTask);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.Repository<Course>()).Returns(courseRepo.Object);
        uow.Setup(u => u.CompleteAsync()).ReturnsAsync(1);

        var svc = new CourseService(uow.Object);

        // Act
        var result = await svc.CreateCourseAsync(dto);

        // Assert
        result.Should().BeTrue();
        courseRepo.Verify(r => r.AddAsync(It.Is<Course>(c => c.Code == dto.Code && c.Title == dto.Title)), Times.Once);
    }

    [Fact]
    public async Task GetAllCoursesAsync_WhenCalled_ReturnsMappedCourseDtos()
    {
        // Arrange
        var deptId = Guid.NewGuid();
        var courses = new List<Course>
        {
            new Course { Id = Guid.NewGuid(), Code = "C201", Title = "Math", Credits = 3, DepartmentId = deptId, AssignedFacultyId = null },
            new Course { Id = Guid.NewGuid(), Code = "C202", Title = "Physics", Credits = 4, DepartmentId = deptId, AssignedFacultyId = Guid.NewGuid() }
        };

        var depts = new List<Department>
        {
            new Department { Id = deptId, Name = "Science" }
        };

        var courseRepo = new Mock<IGenericRepository<Course>>();
        courseRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(courses);

        var deptRepo = new Mock<IGenericRepository<Department>>();
        deptRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(depts);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.Repository<Course>()).Returns(courseRepo.Object);
        uow.Setup(u => u.Repository<Department>()).Returns(deptRepo.Object);

        var svc = new CourseService(uow.Object);

        // Act
        var result = (await svc.GetAllCoursesAsync()).ToList();

        // Assert
        result.Should().HaveCount(2);
        result[0].DepartmentName.Should().Be("Science");
        result[0].AssignedFacultyName.Should().Be("Unassigned");
        result[1].AssignedFacultyName.Should().Be("Assigned");
    }
}
