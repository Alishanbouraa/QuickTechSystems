﻿using AutoMapper;
using Microsoft.EntityFrameworkCore;
using QuickTechSystems.Application.DTOs;
using QuickTechSystems.Application.Services.Interfaces;
using QuickTechSystems.Domain.Entities;
using QuickTechSystems.Domain.Interfaces.Repositories;

namespace QuickTechSystems.Application.Services
{
    public class SupplierService : BaseService<Supplier, SupplierDTO>, ISupplierService
    {
        private new readonly IUnitOfWork _unitOfWork;

        public SupplierService(IUnitOfWork unitOfWork, IMapper mapper)
            : base(unitOfWork, mapper, unitOfWork.Suppliers)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<IEnumerable<SupplierDTO>> GetByNameAsync(string name)
        {
            var suppliers = await _repository.Query()
                .Where(s => s.Name.Contains(name))
                .ToListAsync();
            return _mapper.Map<IEnumerable<SupplierDTO>>(suppliers);
        }

        public async Task<bool> UpdateBalanceAsync(int supplierId, decimal amount)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var supplier = await _repository.GetByIdAsync(supplierId);
                if (supplier == null) return false;

                supplier.Balance += amount;
                supplier.UpdatedAt = DateTime.Now;
                await _repository.UpdateAsync(supplier);
                await _unitOfWork.SaveChangesAsync();

                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<IEnumerable<SupplierDTO>> GetWithOutstandingBalanceAsync()
        {
            var suppliers = await _repository.Query()
                .Where(s => s.Balance > 0)
                .ToListAsync();
            return _mapper.Map<IEnumerable<SupplierDTO>>(suppliers);
        }

        public async Task<IEnumerable<SupplierTransactionDTO>> GetSupplierTransactionsAsync(int supplierId)
        {
            var transactions = await _unitOfWork.Context.Set<SupplierTransaction>()
                .Include(st => st.Supplier)
                .Where(st => st.SupplierId == supplierId)
                .OrderByDescending(st => st.TransactionDate)
                .ToListAsync();
            return _mapper.Map<IEnumerable<SupplierTransactionDTO>>(transactions);
        }

        public async Task<SupplierTransactionDTO> AddTransactionAsync(SupplierTransactionDTO transactionDto)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var supplierTransaction = _mapper.Map<SupplierTransaction>(transactionDto);

                var dbTransaction = await _unitOfWork.Context.Set<SupplierTransaction>()
                    .AddAsync(supplierTransaction);

                // Update supplier balance
                await UpdateBalanceAsync(transactionDto.SupplierId, transactionDto.Amount);

                await _unitOfWork.SaveChangesAsync();
                await transaction.CommitAsync();

                return _mapper.Map<SupplierTransactionDTO>(supplierTransaction);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}