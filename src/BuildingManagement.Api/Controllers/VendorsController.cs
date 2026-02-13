using BuildingManagement.Core.DTOs;
using BuildingManagement.Core.Entities;
using BuildingManagement.Core.Enums;
using BuildingManagement.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BuildingManagement.Api.Controllers;

[ApiController]
[Route("api/vendors")]
[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
public class VendorsController : ControllerBase
{
    private readonly AppDbContext _db;

    public VendorsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<VendorDto>>> GetAll()
    {
        var vendors = await _db.Vendors.Select(v => new VendorDto
        {
            Id = v.Id,
            Name = v.Name,
            ServiceType = v.ServiceType,
            Phone = v.Phone,
            Email = v.Email,
            ContactName = v.ContactName,
            Notes = v.Notes
        }).ToListAsync();
        return Ok(vendors);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<VendorDto>> GetById(int id)
    {
        var vendor = await _db.Vendors.FindAsync(id);
        if (vendor == null) return NotFound();
        return Ok(new VendorDto
        {
            Id = vendor.Id,
            Name = vendor.Name,
            ServiceType = vendor.ServiceType,
            Phone = vendor.Phone,
            Email = vendor.Email,
            ContactName = vendor.ContactName,
            Notes = vendor.Notes
        });
    }

    [HttpPost]
    public async Task<ActionResult<VendorDto>> Create([FromBody] CreateVendorRequest request)
    {
        var vendor = new Vendor
        {
            Name = request.Name,
            ServiceType = request.ServiceType,
            Phone = request.Phone,
            Email = request.Email,
            ContactName = request.ContactName,
            Notes = request.Notes,
            CreatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        };

        _db.Vendors.Add(vendor);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = vendor.Id }, new VendorDto
        {
            Id = vendor.Id,
            Name = vendor.Name,
            ServiceType = vendor.ServiceType,
            Phone = vendor.Phone,
            Email = vendor.Email,
            ContactName = vendor.ContactName,
            Notes = vendor.Notes
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateVendorRequest request)
    {
        var vendor = await _db.Vendors.FindAsync(id);
        if (vendor == null) return NotFound();

        vendor.Name = request.Name;
        vendor.ServiceType = request.ServiceType;
        vendor.Phone = request.Phone;
        vendor.Email = request.Email;
        vendor.ContactName = request.ContactName;
        vendor.Notes = request.Notes;
        vendor.UpdatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        await _db.SaveChangesAsync();
        return NoContent();
    }
}
